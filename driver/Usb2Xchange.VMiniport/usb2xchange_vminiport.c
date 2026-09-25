// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
#include "usb2xchange_vminiport.h"

static const UCHAR Usb2XchangeSrbSignature[8] =
    USB2X_SRB_IO_CONTROL_SIGNATURE;

static BOOLEAN
Usb2XchangeBytesAreZero(
    _In_reads_bytes_(Length) const USB2X_U8 *Bytes,
    _In_ ULONG Length
    )
{
    ULONG index;

    for (index = 0; index < Length; ++index) {
        if (Bytes[index] != 0) {
            return FALSE;
        }
    }
    return TRUE;
}

static BOOLEAN
Usb2XchangeValidateHeader(
    _In_ const USB2X_BROKER_HEADER *Header,
    _In_ USHORT MessageType,
    _In_ ULONG MessageSize
    )
{
    return (Header->Magic == USB2X_BROKER_MAGIC) &&
        (Header->VersionMajor == USB2X_BROKER_VERSION_MAJOR) &&
        (Header->VersionMinor == USB2X_BROKER_VERSION_MINOR) &&
        (Header->MessageType == MessageType) &&
        (Header->HeaderSize == USB2X_BROKER_HEADER_SIZE) &&
        (Header->MessageSize == MessageSize);
}

static BOOLEAN
Usb2XchangeValidateAdapterState(
    _In_ const USB2X_ADAPTER_STATE *State
    )
{
    if (!Usb2XchangeValidateHeader(&State->Header,
            USB2X_MESSAGE_ADAPTER_STATE, USB2X_ADAPTER_STATE_SIZE) ||
        (State->Header.RequestId != 0) ||
        (State->Header.Generation == 0) ||
        (State->Reserved0 != 0)) {
        return FALSE;
    }

    if (State->State == USB2X_ADAPTER_OFFLINE) {
        return (State->PhysicalTargetId == 0) &&
            (State->PhysicalLun == 0) &&
            (State->PeripheralDeviceType == 0) &&
            (State->InquiryLength == 0) &&
            Usb2XchangeBytesAreZero(State->Inquiry,
                USB2X_INQUIRY_LENGTH);
    }

    if (State->State != USB2X_ADAPTER_ONLINE) {
        return FALSE;
    }

    return (State->PhysicalTargetId <= USB2XCHANGE_MAX_PHYSICAL_TARGET) &&
        (State->PhysicalLun == 0) &&
        (State->PeripheralDeviceType == USB2XCHANGE_SCANNER_DEVICE_TYPE) &&
        (State->InquiryLength == USB2X_INQUIRY_LENGTH) &&
        ((State->Inquiry[0] & 0x1FU) == USB2XCHANGE_SCANNER_DEVICE_TYPE);
}

static BOOLEAN
Usb2XchangeValidateServiceWait(
    _In_ const USB2X_SERVICE_WAIT *Wait
    )
{
    return Usb2XchangeValidateHeader(&Wait->Header,
            USB2X_MESSAGE_SERVICE_WAIT, USB2X_SERVICE_WAIT_SIZE) &&
        (Wait->Header.RequestId == 0) &&
        (Wait->Header.Generation != 0);
}

static BOOLEAN
Usb2XchangeValidateSrbIoControl(
    _In_ const SCSI_REQUEST_BLOCK *Srb,
    _Outptr_result_bytebuffer_(*PayloadLength) USB2X_U8 **Payload,
    _Out_ ULONG *PayloadLength,
    _Out_ PSRB_IO_CONTROL *IoControl
    )
{
    PSRB_IO_CONTROL control;

    if ((Srb->DataBuffer == NULL) ||
        (Srb->DataTransferLength < sizeof(SRB_IO_CONTROL))) {
        return FALSE;
    }

    control = (PSRB_IO_CONTROL)Srb->DataBuffer;
    if ((control->HeaderLength != sizeof(SRB_IO_CONTROL)) ||
        (RtlCompareMemory(control->Signature, Usb2XchangeSrbSignature,
            sizeof(control->Signature)) != sizeof(control->Signature)) ||
        (control->Timeout == 0) || (control->Timeout > 300) ||
        (control->ControlCode != USB2X_SRB_CONTROL_BROKER_MESSAGE) ||
        (control->ReturnCode != USB2X_MINIPORT_RETURN_SUCCESS) ||
        (control->Length !=
            (Srb->DataTransferLength - sizeof(SRB_IO_CONTROL)))) {
        return FALSE;
    }

    *Payload = ((USB2X_U8 *)control) + sizeof(SRB_IO_CONTROL);
    *PayloadLength = control->Length;
    *IoControl = control;
    return TRUE;
}

static VOID
Usb2XchangeCompleteSrb(
    _In_ PVOID DeviceExtension,
    _Inout_ PSCSI_REQUEST_BLOCK Srb,
    _In_ UCHAR SrbStatus,
    _In_ UCHAR ScsiStatus,
    _In_ ULONG TransferLength
    )
{
    Srb->ScsiStatus = ScsiStatus;
    Srb->DataTransferLength = TransferLength;
    Srb->SrbStatus = SrbStatus;
    StorPortNotification(RequestComplete, DeviceExtension, Srb);
}

static VOID
Usb2XchangeCompleteServiceIrp(
    _In_ PVOID DeviceExtension,
    _Inout_ PIRP Irp,
    _In_ NTSTATUS Status,
    _In_ ULONG_PTR Information
    )
{
    Irp->IoStatus.Status = Status;
    Irp->IoStatus.Information = Information;
    (VOID)StorPortCompleteServiceIrp(DeviceExtension, Irp);
}

static PIRP
Usb2XchangeDetachServiceIrpLocked(
    _Inout_ PUSB2XCHANGE_ADAPTER_EXTENSION AdapterExtension
    )
{
    PIRP irp;

    irp = AdapterExtension->ServiceIrp;
    AdapterExtension->ServiceIrp = NULL;
    return irp;
}

static PSCSI_REQUEST_BLOCK
Usb2XchangeDetachPendingSrbLocked(
    _Inout_ PUSB2XCHANGE_ADAPTER_EXTENSION AdapterExtension
    )
{
    PSCSI_REQUEST_BLOCK srb;

    srb = AdapterExtension->PendingSrb;
    AdapterExtension->PendingSrb = NULL;
    AdapterExtension->PendingRequestPublished = FALSE;
    RtlZeroMemory(&AdapterExtension->PendingRequest,
        sizeof(AdapterExtension->PendingRequest));
    return srb;
}

static UCHAR
Usb2XchangeDirectionFromSrb(
    _In_ const SCSI_REQUEST_BLOCK *Srb
    )
{
    ULONG flags;

    flags = Srb->SrbFlags & (SRB_FLAGS_DATA_IN | SRB_FLAGS_DATA_OUT);
    if (flags == SRB_FLAGS_DATA_IN) {
        return USB2X_DIRECTION_IN;
    }
    if (flags == 0) {
        return USB2X_DIRECTION_NONE;
    }
    return USB2X_DIRECTION_OUT;
}

static BOOLEAN
Usb2XchangeBuildRequest(
    _In_ const USB2XCHANGE_ADAPTER_EXTENSION *AdapterExtension,
    _In_ const SCSI_REQUEST_BLOCK *Srb,
    _In_ USB2X_U64 RequestId,
    _Out_ USB2X_SCSI_REQUEST *Request
    )
{
    UCHAR direction;
    ULONG dataFlags;
    ULONG dataLength;
    ULONG timeoutMilliseconds;

    if ((Srb->PathId != 0) || (Srb->TargetId != 0) || (Srb->Lun != 0) ||
        (Srb->CdbLength == 0) ||
        (Srb->CdbLength > USB2X_MAX_CDB_LENGTH) ||
        (Srb->DataTransferLength > USB2X_MAX_TRANSFER_LENGTH) ||
        (Srb->TimeOutValue == 0) ||
        (Srb->TimeOutValue > USB2X_MAX_SRB_TIMEOUT_SECONDS)) {
        return FALSE;
    }

    dataFlags = Srb->SrbFlags &
        (SRB_FLAGS_DATA_IN | SRB_FLAGS_DATA_OUT);
    if (dataFlags == (SRB_FLAGS_DATA_IN | SRB_FLAGS_DATA_OUT)) {
        return FALSE;
    }
    direction = Usb2XchangeDirectionFromSrb(Srb);
    if ((direction == USB2X_DIRECTION_NONE) &&
        (Srb->DataTransferLength != 0)) {
        return FALSE;
    }
    if ((Srb->DataTransferLength != 0) && (Srb->DataBuffer == NULL)) {
        return FALSE;
    }
    dataLength = (direction == USB2X_DIRECTION_OUT) ?
        Srb->DataTransferLength : 0;

    timeoutMilliseconds = Srb->TimeOutValue * 1000U;
    if (timeoutMilliseconds > USB2X_MAX_TIMEOUT_MS) {
        timeoutMilliseconds = USB2X_MAX_TIMEOUT_MS;
    }
    RtlZeroMemory(Request, sizeof(*Request));
    Request->Header.Magic = USB2X_BROKER_MAGIC;
    Request->Header.VersionMajor = USB2X_BROKER_VERSION_MAJOR;
    Request->Header.VersionMinor = USB2X_BROKER_VERSION_MINOR;
    Request->Header.MessageType = USB2X_MESSAGE_SCSI_REQUEST;
    Request->Header.HeaderSize = USB2X_BROKER_HEADER_SIZE;
    Request->Header.MessageSize =
        USB2X_SCSI_REQUEST_PREFIX_SIZE + dataLength;
    Request->Header.RequestId = RequestId;
    Request->Header.Generation = AdapterExtension->Generation;
    Request->PathId = Srb->PathId;
    Request->TargetId = Srb->TargetId;
    Request->Lun = Srb->Lun;
    Request->PhysicalTargetId = AdapterExtension->PhysicalTargetId;
    Request->PhysicalLun = AdapterExtension->PhysicalLun;
    Request->CdbLength = Srb->CdbLength;
    Request->Direction = direction;
    Request->SrbFunction = USB2X_SRB_FUNCTION_EXECUTE_SCSI;
    Request->TransferLength = Srb->DataTransferLength;
    Request->TimeoutMilliseconds = timeoutMilliseconds;
    if (Srb->SenseInfoBuffer == NULL) {
        Request->SenseAllocationLength = 0;
    } else {
        Request->SenseAllocationLength =
            (Srb->SenseInfoBufferLength < USB2X_MAX_SENSE_LENGTH) ?
            Srb->SenseInfoBufferLength : USB2X_MAX_SENSE_LENGTH;
    }
    RtlCopyMemory(Request->Cdb, Srb->Cdb, Srb->CdbLength);
    Request->DataLength = dataLength;
    if (dataLength != 0) {
        RtlCopyMemory(Request->Data, Srb->DataBuffer, dataLength);
    }
    return TRUE;
}

static BOOLEAN
Usb2XchangeValidateCompletion(
    _In_ const USB2X_SCSI_REQUEST *Request,
    _In_reads_bytes_(PayloadLength) const USB2X_SCSI_COMPLETION *Completion,
    _In_ ULONG PayloadLength
    )
{
    ULONG expectedDataLength;

    if ((PayloadLength < USB2X_SCSI_COMPLETION_PREFIX_SIZE) ||
        (PayloadLength > USB2X_SCSI_COMPLETION_MAX_SIZE) ||
        !Usb2XchangeValidateHeader(&Completion->Header,
            USB2X_MESSAGE_SCSI_COMPLETION, PayloadLength) ||
        (Completion->Header.RequestId != Request->Header.RequestId) ||
        (Completion->Header.Generation != Request->Header.Generation) ||
        (Completion->TransportStatus > USB2X_TRANSPORT_STALE_GENERATION) ||
        (Completion->SenseLength > USB2X_MAX_SENSE_LENGTH) ||
        (Completion->SenseLength > Request->SenseAllocationLength) ||
        (Completion->Reserved0 != 0) || (Completion->Reserved1 != 0) ||
        !Usb2XchangeBytesAreZero(
            Completion->Sense + Completion->SenseLength,
            USB2X_MAX_SENSE_LENGTH - Completion->SenseLength) ||
        (Completion->DataLength > USB2X_MAX_TRANSFER_LENGTH) ||
        (Completion->DataLength !=
            (PayloadLength - USB2X_SCSI_COMPLETION_PREFIX_SIZE))) {
        return FALSE;
    }

    if (Completion->TransportStatus != USB2X_TRANSPORT_SUCCESS) {
        return (Completion->AdapterStatus == 0) &&
            (Completion->ScsiStatus == 0) &&
            (Completion->SenseLength == 0) &&
            (Completion->TransferLength == 0) &&
            (Completion->Residue == 0) &&
            (Completion->DataLength == 0);
    }

    if ((Completion->TransferLength > Request->TransferLength) ||
        (Completion->Residue !=
            (Request->TransferLength - Completion->TransferLength))) {
        return FALSE;
    }
    expectedDataLength = (Request->Direction == USB2X_DIRECTION_IN) ?
        Completion->TransferLength : 0;
    if (Completion->DataLength != expectedDataLength) {
        return FALSE;
    }

    switch (Completion->AdapterStatus) {
    case 0x00:
        return (Completion->ScsiStatus == SCSISTAT_GOOD) &&
            (Completion->SenseLength == 0);
    case 0x02:
        return Completion->ScsiStatus == SCSISTAT_CHECK_CONDITION;
    case 0x08:
        return (Completion->ScsiStatus == SCSISTAT_BUSY) &&
            (Completion->SenseLength == 0) &&
            (Completion->TransferLength == 0);
    case 0x8A:
        return (Completion->ScsiStatus == 0) &&
            (Completion->SenseLength == 0) &&
            (Completion->TransferLength == 0);
    default:
        return FALSE;
    }
}

static UCHAR
Usb2XchangeMapTransportStatus(
    _In_ ULONG TransportStatus
    )
{
    switch (TransportStatus) {
    case USB2X_TRANSPORT_NO_DEVICE:
        return SRB_STATUS_NO_DEVICE;
    case USB2X_TRANSPORT_TIMEOUT:
        return SRB_STATUS_TIMEOUT;
    case USB2X_TRANSPORT_CANCELLED:
        return SRB_STATUS_ABORTED;
    case USB2X_TRANSPORT_BLOCKED:
        return SRB_STATUS_INVALID_REQUEST;
    case USB2X_TRANSPORT_STALE_GENERATION:
        return SRB_STATUS_REQUEST_FLUSHED;
    case USB2X_TRANSPORT_IO_ERROR:
    case USB2X_TRANSPORT_PROTOCOL_ERROR:
    default:
        return SRB_STATUS_ERROR;
    }
}

static VOID
Usb2XchangeFinishCompletion(
    _In_ PVOID DeviceExtension,
    _Inout_ PSCSI_REQUEST_BLOCK Srb,
    _In_ const USB2X_SCSI_COMPLETION *Completion
    )
{
    UCHAR srbStatus;
    UCHAR scsiStatus;
    ULONG transferLength;
    ULONG senseLength;

    srbStatus = SRB_STATUS_ERROR;
    scsiStatus = 0;
    transferLength = 0;

    if (Completion->TransportStatus != USB2X_TRANSPORT_SUCCESS) {
        srbStatus = Usb2XchangeMapTransportStatus(
            Completion->TransportStatus);
    } else {
        switch (Completion->AdapterStatus) {
        case 0x00:
            srbStatus = SRB_STATUS_SUCCESS;
            break;
        case 0x02:
            srbStatus = SRB_STATUS_ERROR;
            break;
        case 0x08:
            srbStatus = SRB_STATUS_BUSY;
            break;
        case 0x8A:
            srbStatus = SRB_STATUS_SELECTION_TIMEOUT;
            break;
        default:
            srbStatus = SRB_STATUS_ERROR;
            break;
        }

        scsiStatus = Completion->ScsiStatus;
        transferLength = Completion->TransferLength;
        if (Completion->DataLength != 0) {
            RtlCopyMemory(Srb->DataBuffer, Completion->Data,
                Completion->DataLength);
        }
        senseLength = Completion->SenseLength;
        if ((senseLength != 0) && (Srb->SenseInfoBuffer != NULL)) {
            RtlCopyMemory(Srb->SenseInfoBuffer, Completion->Sense,
                senseLength);
            srbStatus |= SRB_STATUS_AUTOSENSE_VALID;
        }
    }

    Usb2XchangeCompleteSrb(DeviceExtension, Srb, srbStatus, scsiStatus,
        transferLength);
}

static VOID
Usb2XchangeHandleAdapterState(
    _In_ PVOID DeviceExtension,
    _In_ const USB2X_ADAPTER_STATE *State,
    _Inout_ PSRB_IO_CONTROL IoControl
    )
{
    PUSB2XCHANGE_ADAPTER_EXTENSION adapterExtension;
    STOR_LOCK_HANDLE lockHandle;
    PSCSI_REQUEST_BLOCK pendingSrb;
    PIRP serviceIrp;
    BOOLEAN oldOnline;
    BOOLEAN notifyBusChange;

    adapterExtension = (PUSB2XCHANGE_ADAPTER_EXTENSION)DeviceExtension;
    pendingSrb = NULL;
    serviceIrp = NULL;
    notifyBusChange = FALSE;

    StorPortAcquireSpinLock(DeviceExtension, StartIoLock, NULL, &lockHandle);
    if ((!adapterExtension->Initialized) || adapterExtension->Stopped) {
        IoControl->ReturnCode = USB2X_MINIPORT_RETURN_NOT_READY;
    } else if (State->Header.Generation <= adapterExtension->Generation) {
        IoControl->ReturnCode = USB2X_MINIPORT_RETURN_STALE_GENERATION;
    } else {
        oldOnline = adapterExtension->Online;
        adapterExtension->Generation = State->Header.Generation;
        adapterExtension->Online =
            (State->State == USB2X_ADAPTER_ONLINE) ? TRUE : FALSE;
        adapterExtension->PhysicalTargetId = State->PhysicalTargetId;
        adapterExtension->PhysicalLun = State->PhysicalLun;
        RtlCopyMemory(adapterExtension->Inquiry, State->Inquiry,
            USB2X_INQUIRY_LENGTH);
        pendingSrb = Usb2XchangeDetachPendingSrbLocked(adapterExtension);
        serviceIrp = Usb2XchangeDetachServiceIrpLocked(adapterExtension);
        notifyBusChange = (oldOnline != adapterExtension->Online);
        IoControl->ReturnCode = USB2X_MINIPORT_RETURN_SUCCESS;
    }
    StorPortReleaseSpinLock(DeviceExtension, &lockHandle);

    if (pendingSrb != NULL) {
        Usb2XchangeCompleteSrb(DeviceExtension, pendingSrb,
            SRB_STATUS_REQUEST_FLUSHED, 0, 0);
    }
    if (serviceIrp != NULL) {
        Usb2XchangeCompleteServiceIrp(DeviceExtension, serviceIrp,
            STATUS_DEVICE_NOT_READY, 0);
    }
    if (notifyBusChange) {
        StorPortNotification(BusChangeDetected, DeviceExtension, 0);
    }
}

static VOID
Usb2XchangeHandleCompletion(
    _In_ PVOID DeviceExtension,
    _In_reads_bytes_(PayloadLength) const USB2X_SCSI_COMPLETION *Completion,
    _In_ ULONG PayloadLength,
    _Inout_ PSRB_IO_CONTROL IoControl
    )
{
    PUSB2XCHANGE_ADAPTER_EXTENSION adapterExtension;
    STOR_LOCK_HANDLE lockHandle;
    PSCSI_REQUEST_BLOCK pendingSrb;

    adapterExtension = (PUSB2XCHANGE_ADAPTER_EXTENSION)DeviceExtension;
    pendingSrb = NULL;

    StorPortAcquireSpinLock(DeviceExtension, StartIoLock, NULL, &lockHandle);
    if ((!adapterExtension->Initialized) || adapterExtension->Stopped) {
        IoControl->ReturnCode = USB2X_MINIPORT_RETURN_NOT_READY;
    } else if ((adapterExtension->PendingSrb == NULL) ||
        !adapterExtension->PendingRequestPublished) {
        IoControl->ReturnCode = USB2X_MINIPORT_RETURN_NO_PENDING;
    } else if ((Completion->Header.Generation !=
            adapterExtension->Generation) ||
        (Completion->Header.Generation !=
            adapterExtension->PendingRequest.Header.Generation)) {
        IoControl->ReturnCode = USB2X_MINIPORT_RETURN_STALE_GENERATION;
    } else if (!Usb2XchangeValidateCompletion(
            &adapterExtension->PendingRequest, Completion, PayloadLength)) {
        IoControl->ReturnCode = USB2X_MINIPORT_RETURN_INVALID_MESSAGE;
    } else {
        pendingSrb = Usb2XchangeDetachPendingSrbLocked(adapterExtension);
        IoControl->ReturnCode = USB2X_MINIPORT_RETURN_SUCCESS;
    }
    StorPortReleaseSpinLock(DeviceExtension, &lockHandle);

    if (pendingSrb != NULL) {
        Usb2XchangeFinishCompletion(DeviceExtension, pendingSrb, Completion);
    }
}

static VOID
Usb2XchangeHandleIoControl(
    _In_ PVOID DeviceExtension,
    _Inout_ PSCSI_REQUEST_BLOCK Srb
    )
{
    USB2X_U8 *payload;
    ULONG payloadLength;
    PSRB_IO_CONTROL ioControl;
    const USB2X_BROKER_HEADER *header;

    if (!Usb2XchangeValidateSrbIoControl(Srb, &payload, &payloadLength,
            &ioControl) || (payloadLength < USB2X_BROKER_HEADER_SIZE)) {
        Usb2XchangeCompleteSrb(DeviceExtension, Srb,
            SRB_STATUS_INVALID_REQUEST, 0, 0);
        return;
    }

    ioControl->ReturnCode = USB2X_MINIPORT_RETURN_INVALID_MESSAGE;
    header = (const USB2X_BROKER_HEADER *)payload;
    if ((header->MessageType == USB2X_MESSAGE_ADAPTER_STATE) &&
        (payloadLength == USB2X_ADAPTER_STATE_SIZE) &&
        Usb2XchangeValidateAdapterState(
            (const USB2X_ADAPTER_STATE *)payload)) {
        Usb2XchangeHandleAdapterState(DeviceExtension,
            (const USB2X_ADAPTER_STATE *)payload, ioControl);
    } else if ((header->MessageType == USB2X_MESSAGE_SCSI_COMPLETION) &&
        (payloadLength >= USB2X_SCSI_COMPLETION_PREFIX_SIZE) &&
        (payloadLength <= USB2X_SCSI_COMPLETION_MAX_SIZE)) {
        Usb2XchangeHandleCompletion(DeviceExtension,
            (const USB2X_SCSI_COMPLETION *)payload, payloadLength,
            ioControl);
    }

    Usb2XchangeCompleteSrb(DeviceExtension, Srb, SRB_STATUS_SUCCESS, 0,
        Srb->DataTransferLength);
}

static BOOLEAN
Usb2XchangeQueueScsiRequest(
    _In_ PVOID DeviceExtension,
    _Inout_ PSCSI_REQUEST_BLOCK Srb
    )
{
    PUSB2XCHANGE_ADAPTER_EXTENSION adapterExtension;
    STOR_LOCK_HANDLE lockHandle;
    USB2X_U64 requestId;
    PIRP serviceIrp;
    NTSTATUS serviceStatus;
    ULONG_PTR serviceInformation;
    UCHAR immediateSrbStatus;
    BOOLEAN queued;

    adapterExtension = (PUSB2XCHANGE_ADAPTER_EXTENSION)DeviceExtension;
    serviceIrp = NULL;
    serviceStatus = STATUS_SUCCESS;
    serviceInformation = 0;
    immediateSrbStatus = SRB_STATUS_INVALID_REQUEST;
    queued = FALSE;

    StorPortAcquireSpinLock(DeviceExtension, StartIoLock, NULL, &lockHandle);
    if ((!adapterExtension->Initialized) || adapterExtension->Stopped ||
        !adapterExtension->Online) {
        immediateSrbStatus = SRB_STATUS_NO_DEVICE;
    } else if (adapterExtension->PendingSrb != NULL) {
        immediateSrbStatus = SRB_STATUS_BUSY;
    } else {
        requestId = adapterExtension->NextRequestId + 1;
        if (requestId == 0) {
            requestId = 1;
        }
        if (Usb2XchangeBuildRequest(adapterExtension, Srb, requestId,
                &adapterExtension->PendingRequest)) {
            adapterExtension->NextRequestId = requestId;
            adapterExtension->PendingSrb = Srb;
            adapterExtension->PendingRequestPublished = FALSE;
            serviceIrp = Usb2XchangeDetachServiceIrpLocked(
                adapterExtension);
            if (serviceIrp != NULL) {
                RtlCopyMemory(serviceIrp->AssociatedIrp.SystemBuffer,
                    &adapterExtension->PendingRequest,
                    adapterExtension->PendingRequest.Header.MessageSize);
                serviceInformation =
                    adapterExtension->PendingRequest.Header.MessageSize;
                adapterExtension->PendingRequestPublished = TRUE;
            }
            queued = TRUE;
        }
    }
    StorPortReleaseSpinLock(DeviceExtension, &lockHandle);

    if (serviceIrp != NULL) {
        Usb2XchangeCompleteServiceIrp(DeviceExtension, serviceIrp,
            serviceStatus, serviceInformation);
    }
    if (!queued) {
        Usb2XchangeCompleteSrb(DeviceExtension, Srb, immediateSrbStatus,
            0, 0);
    }
    return TRUE;
}

NTSTATUS
DriverEntry(
    _In_ PDRIVER_OBJECT DriverObject,
    _In_ PUNICODE_STRING RegistryPath
    )
{
    HW_INITIALIZATION_DATA initializationData;

    RtlZeroMemory(&initializationData, sizeof(initializationData));
    initializationData.HwInitializationDataSize = sizeof(initializationData);
    initializationData.AdapterInterfaceType = Internal;
    initializationData.HwInitialize = Usb2XchangeHwInitialize;
    initializationData.HwStartIo = Usb2XchangeHwStartIo;
    initializationData.HwFindAdapter = (PVOID)Usb2XchangeVirtualHwFindAdapter;
    initializationData.HwResetBus = Usb2XchangeHwResetBus;
    initializationData.HwAdapterControl = Usb2XchangeHwAdapterControl;
    initializationData.HwFreeAdapterResources =
        Usb2XchangeHwFreeAdapterResources;
    initializationData.HwProcessServiceRequest =
        Usb2XchangeHwProcessServiceRequest;
    initializationData.HwCompleteServiceIrp =
        Usb2XchangeHwCompleteServiceIrp;
    initializationData.DeviceExtensionSize =
        sizeof(USB2XCHANGE_ADAPTER_EXTENSION);
    initializationData.MapBuffers = STOR_MAP_ALL_BUFFERS_INCLUDING_READ_WRITE;
    initializationData.NeedPhysicalAddresses = TRUE;
    initializationData.TaggedQueuing = TRUE;
    initializationData.AutoRequestSense = TRUE;
    initializationData.MultipleRequestPerLu = TRUE;
    initializationData.FeatureSupport = STOR_FEATURE_VIRTUAL_MINIPORT;
    initializationData.SrbTypeFlags = SRB_TYPE_FLAG_SCSI_REQUEST_BLOCK;
    initializationData.AddressTypeFlags = ADDRESS_TYPE_FLAG_BTL8;

    return (NTSTATUS)StorPortInitialize(DriverObject, RegistryPath,
        &initializationData, NULL);
}

ULONG
Usb2XchangeVirtualHwFindAdapter(
    _In_ PVOID DeviceExtension,
    _In_ PVOID HwContext,
    _In_ PVOID BusInformation,
    _In_ PVOID LowerDevice,
    _In_ PCHAR ArgumentString,
    _Inout_ PPORT_CONFIGURATION_INFORMATION ConfigInfo,
    _In_ PBOOLEAN Again
    )
{
    PUSB2XCHANGE_ADAPTER_EXTENSION adapterExtension;

    UNREFERENCED_PARAMETER(HwContext);
    UNREFERENCED_PARAMETER(BusInformation);
    UNREFERENCED_PARAMETER(LowerDevice);
    UNREFERENCED_PARAMETER(ArgumentString);

    if ((DeviceExtension == NULL) || (ConfigInfo == NULL) ||
        (Again == NULL)) {
        return SP_RETURN_BAD_CONFIG;
    }

    adapterExtension = (PUSB2XCHANGE_ADAPTER_EXTENSION)DeviceExtension;
    RtlZeroMemory(adapterExtension, sizeof(*adapterExtension));
    *Again = FALSE;
    ConfigInfo->MaximumTransferLength = USB2XCHANGE_MAX_TRANSFER_BYTES;
    // FlexColor's loader wraps each 4096-byte firmware chunk in a ten-byte
    // command header. A 4112-byte transfer can span three pages when the
    // caller's buffer is maximally unaligned.
    ConfigInfo->NumberOfPhysicalBreaks =
        USB2XCHANGE_MAX_PHYSICAL_BREAKS;
    ConfigInfo->AlignmentMask = 0;
    ConfigInfo->NumberOfAccessRanges = 0;
    ConfigInfo->NumberOfBuses = USB2XCHANGE_NUMBER_OF_BUSES;
    ConfigInfo->MaximumNumberOfTargets = USB2XCHANGE_MAX_TARGETS;
    ConfigInfo->InitiatorBusId[0] = USB2XCHANGE_INITIATOR_ID;
    ConfigInfo->MaximumNumberOfLogicalUnits = USB2XCHANGE_MAX_LUNS;
    ConfigInfo->SynchronizationModel = StorSynchronizeFullDuplex;
    ConfigInfo->VirtualDevice = TRUE;
    // One target SRB may be retained while the broker submits its completion
    // through a concurrent SRB_FUNCTION_IO_CONTROL. The extension still
    // enforces exactly one outstanding execute-SCSI request.
    ConfigInfo->MaxNumberOfIO = 2;
    ConfigInfo->MaxIOsPerLun = 1;
    ConfigInfo->InitialLunQueueDepth = 1;

    return SP_RETURN_FOUND;
}

BOOLEAN
Usb2XchangeHwInitialize(
    _In_ PVOID DeviceExtension
    )
{
    PUSB2XCHANGE_ADAPTER_EXTENSION adapterExtension;

    if (DeviceExtension == NULL) {
        return FALSE;
    }
    adapterExtension = (PUSB2XCHANGE_ADAPTER_EXTENSION)DeviceExtension;
    adapterExtension->Initialized = TRUE;
    adapterExtension->Stopped = FALSE;
    return TRUE;
}

BOOLEAN
Usb2XchangeHwStartIo(
    _In_ PVOID DeviceExtension,
    _In_ PSCSI_REQUEST_BLOCK Srb
    )
{
    if ((DeviceExtension == NULL) || (Srb == NULL)) {
        return FALSE;
    }

    if (Srb->Function == SRB_FUNCTION_EXECUTE_SCSI) {
        return Usb2XchangeQueueScsiRequest(DeviceExtension, Srb);
    }
    if (Srb->Function == SRB_FUNCTION_IO_CONTROL) {
        Usb2XchangeHandleIoControl(DeviceExtension, Srb);
        return TRUE;
    }

    Usb2XchangeCompleteSrb(DeviceExtension, Srb, SRB_STATUS_INVALID_REQUEST,
        0, 0);
    return TRUE;
}

VOID
Usb2XchangeHwProcessServiceRequest(
    _In_ PVOID DeviceExtension,
    _In_ PVOID Irp
    )
{
    PUSB2XCHANGE_ADAPTER_EXTENSION adapterExtension;
    PIRP serviceIrp;
    PIO_STACK_LOCATION stack;
    PVOID systemBuffer;
    ULONG inputLength;
    ULONG outputLength;
    STOR_LOCK_HANDLE lockHandle;
    NTSTATUS status;
    ULONG_PTR information;
    BOOLEAN completeNow;

    if ((DeviceExtension == NULL) || (Irp == NULL)) {
        return;
    }

    adapterExtension = (PUSB2XCHANGE_ADAPTER_EXTENSION)DeviceExtension;
    serviceIrp = (PIRP)Irp;
    stack = IoGetCurrentIrpStackLocation(serviceIrp);
    systemBuffer = serviceIrp->AssociatedIrp.SystemBuffer;
    inputLength = stack->Parameters.DeviceIoControl.InputBufferLength;
    outputLength = stack->Parameters.DeviceIoControl.OutputBufferLength;
    status = STATUS_INVALID_PARAMETER;
    information = 0;
    completeNow = TRUE;

    if ((systemBuffer == NULL) ||
        (inputLength != USB2X_SERVICE_WAIT_SIZE) ||
        (outputLength < USB2X_SCSI_REQUEST_MAX_SIZE) ||
        !Usb2XchangeValidateServiceWait(
            (const USB2X_SERVICE_WAIT *)systemBuffer)) {
        Usb2XchangeCompleteServiceIrp(DeviceExtension, serviceIrp, status,
            information);
        return;
    }

    StorPortAcquireSpinLock(DeviceExtension, StartIoLock, NULL, &lockHandle);
    if ((!adapterExtension->Initialized) || adapterExtension->Stopped ||
        (adapterExtension->Generation == 0)) {
        status = STATUS_DEVICE_NOT_READY;
    } else if (((const USB2X_SERVICE_WAIT *)systemBuffer)->Header.Generation !=
        adapterExtension->Generation) {
        status = STATUS_REVISION_MISMATCH;
    } else if (adapterExtension->ServiceIrp != NULL) {
        status = STATUS_DEVICE_BUSY;
    } else if ((adapterExtension->PendingSrb != NULL) &&
        !adapterExtension->PendingRequestPublished) {
        RtlCopyMemory(systemBuffer, &adapterExtension->PendingRequest,
            adapterExtension->PendingRequest.Header.MessageSize);
        adapterExtension->PendingRequestPublished = TRUE;
        status = STATUS_SUCCESS;
        information = adapterExtension->PendingRequest.Header.MessageSize;
    } else {
        adapterExtension->ServiceIrp = serviceIrp;
        completeNow = FALSE;
    }
    StorPortReleaseSpinLock(DeviceExtension, &lockHandle);

    if (completeNow) {
        Usb2XchangeCompleteServiceIrp(DeviceExtension, serviceIrp, status,
            information);
    }
}

VOID
Usb2XchangeHwCompleteServiceIrp(
    _In_ PVOID DeviceExtension
    )
{
    PUSB2XCHANGE_ADAPTER_EXTENSION adapterExtension;
    STOR_LOCK_HANDLE lockHandle;
    PIRP serviceIrp;

    if (DeviceExtension == NULL) {
        return;
    }
    adapterExtension = (PUSB2XCHANGE_ADAPTER_EXTENSION)DeviceExtension;
    StorPortAcquireSpinLock(DeviceExtension, StartIoLock, NULL, &lockHandle);
    serviceIrp = Usb2XchangeDetachServiceIrpLocked(adapterExtension);
    StorPortReleaseSpinLock(DeviceExtension, &lockHandle);
    if (serviceIrp != NULL) {
        Usb2XchangeCompleteServiceIrp(DeviceExtension, serviceIrp,
            STATUS_DEVICE_REMOVED, 0);
    }
}

BOOLEAN
Usb2XchangeHwResetBus(
    _In_ PVOID DeviceExtension,
    _In_ ULONG PathId
    )
{
    PUSB2XCHANGE_ADAPTER_EXTENSION adapterExtension;
    STOR_LOCK_HANDLE lockHandle;
    PSCSI_REQUEST_BLOCK pendingSrb;
    PIRP serviceIrp;

    if ((DeviceExtension == NULL) || (PathId != 0)) {
        return FALSE;
    }
    adapterExtension = (PUSB2XCHANGE_ADAPTER_EXTENSION)DeviceExtension;
    StorPortAcquireSpinLock(DeviceExtension, StartIoLock, NULL, &lockHandle);
    pendingSrb = Usb2XchangeDetachPendingSrbLocked(adapterExtension);
    serviceIrp = Usb2XchangeDetachServiceIrpLocked(adapterExtension);
    StorPortReleaseSpinLock(DeviceExtension, &lockHandle);
    if (pendingSrb != NULL) {
        Usb2XchangeCompleteSrb(DeviceExtension, pendingSrb,
            SRB_STATUS_BUS_RESET, 0, 0);
    }
    if (serviceIrp != NULL) {
        Usb2XchangeCompleteServiceIrp(DeviceExtension, serviceIrp,
            STATUS_CANCELLED, 0);
    }
    return TRUE;
}

static VOID
Usb2XchangeStopAdapter(
    _In_ PVOID DeviceExtension,
    _In_ NTSTATUS ServiceStatus
    )
{
    PUSB2XCHANGE_ADAPTER_EXTENSION adapterExtension;
    STOR_LOCK_HANDLE lockHandle;
    PSCSI_REQUEST_BLOCK pendingSrb;
    PIRP serviceIrp;
    BOOLEAN wasOnline;

    adapterExtension = (PUSB2XCHANGE_ADAPTER_EXTENSION)DeviceExtension;
    StorPortAcquireSpinLock(DeviceExtension, StartIoLock, NULL, &lockHandle);
    wasOnline = adapterExtension->Online;
    adapterExtension->Stopped = TRUE;
    adapterExtension->Online = FALSE;
    pendingSrb = Usb2XchangeDetachPendingSrbLocked(adapterExtension);
    serviceIrp = Usb2XchangeDetachServiceIrpLocked(adapterExtension);
    StorPortReleaseSpinLock(DeviceExtension, &lockHandle);
    if (pendingSrb != NULL) {
        Usb2XchangeCompleteSrb(DeviceExtension, pendingSrb,
            SRB_STATUS_NO_DEVICE, 0, 0);
    }
    if (serviceIrp != NULL) {
        Usb2XchangeCompleteServiceIrp(DeviceExtension, serviceIrp,
            ServiceStatus, 0);
    }
    if (wasOnline) {
        StorPortNotification(BusChangeDetected, DeviceExtension, 0);
    }
}

SCSI_ADAPTER_CONTROL_STATUS
Usb2XchangeHwAdapterControl(
    _In_ PVOID DeviceExtension,
    _In_ SCSI_ADAPTER_CONTROL_TYPE ControlType,
    _In_ PVOID Parameters
    )
{
    PUSB2XCHANGE_ADAPTER_EXTENSION adapterExtension;
    PSCSI_SUPPORTED_CONTROL_TYPE_LIST supportedTypes;
    ULONG index;
    ULONG knownControlTypeCount;

    if (DeviceExtension == NULL) {
        return ScsiAdapterControlUnsuccessful;
    }
    adapterExtension = (PUSB2XCHANGE_ADAPTER_EXTENSION)DeviceExtension;

    switch (ControlType) {
    case ScsiQuerySupportedControlTypes:
        if (Parameters == NULL) {
            return ScsiAdapterControlUnsuccessful;
        }
        supportedTypes = (PSCSI_SUPPORTED_CONTROL_TYPE_LIST)Parameters;
        knownControlTypeCount = supportedTypes->MaxControlType;
        if (knownControlTypeCount > (ULONG)ScsiAdapterControlMax) {
            knownControlTypeCount = (ULONG)ScsiAdapterControlMax;
        }
        for (index = 0; index < knownControlTypeCount; ++index) {
            supportedTypes->SupportedTypeList[index] = FALSE;
        }
        if ((ULONG)ScsiQuerySupportedControlTypes <
            supportedTypes->MaxControlType) {
            supportedTypes->SupportedTypeList[
                ScsiQuerySupportedControlTypes] = TRUE;
        }
        if ((ULONG)ScsiStopAdapter < supportedTypes->MaxControlType) {
            supportedTypes->SupportedTypeList[ScsiStopAdapter] = TRUE;
        }
        if ((ULONG)ScsiRestartAdapter < supportedTypes->MaxControlType) {
            supportedTypes->SupportedTypeList[ScsiRestartAdapter] = TRUE;
        }
        return ScsiAdapterControlSuccess;

    case ScsiStopAdapter:
        Usb2XchangeStopAdapter(DeviceExtension, STATUS_DEVICE_NOT_READY);
        return ScsiAdapterControlSuccess;

    case ScsiRestartAdapter:
    {
        STOR_LOCK_HANDLE lockHandle;

        StorPortAcquireSpinLock(DeviceExtension, StartIoLock, NULL,
            &lockHandle);
        adapterExtension->Stopped = FALSE;
        StorPortReleaseSpinLock(DeviceExtension, &lockHandle);
        return ScsiAdapterControlSuccess;
    }

    default:
        return ScsiAdapterControlUnsuccessful;
    }
}

VOID
Usb2XchangeHwFreeAdapterResources(
    _In_ PVOID DeviceExtension
    )
{
    PUSB2XCHANGE_ADAPTER_EXTENSION adapterExtension;

    if (DeviceExtension == NULL) {
        return;
    }
    Usb2XchangeStopAdapter(DeviceExtension, STATUS_DEVICE_REMOVED);
    adapterExtension = (PUSB2XCHANGE_ADAPTER_EXTENSION)DeviceExtension;
    adapterExtension->Initialized = FALSE;
}
