// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
#pragma once

/*
 * Pointer-free wire contract between usb2xchange-vminiport.sys and the
 * LocalSystem broker. Multi-byte values are little-endian. The structs use
 * only fixed-width scalar fields and byte arrays; never add a native pointer,
 * handle, size_t, enum, or compiler-dependent Boolean to this ABI.
 */

typedef unsigned char USB2X_U8;
typedef unsigned short USB2X_U16;
typedef unsigned long USB2X_U32;
typedef unsigned __int64 USB2X_U64;

#define USB2X_BROKER_MAGIC                 0x58423255UL /* "U2BX" */
#define USB2X_BROKER_VERSION_MAJOR         1U
#define USB2X_BROKER_VERSION_MINOR         2U
#define USB2X_SRB_IO_CONTROL_SIGNATURE      "U2XCHG1"
#define USB2X_SRB_CONTROL_BROKER_MESSAGE    1U

#define USB2X_MESSAGE_SCSI_REQUEST         1U
#define USB2X_MESSAGE_SCSI_COMPLETION      2U
#define USB2X_MESSAGE_ADAPTER_STATE        3U
#define USB2X_MESSAGE_SERVICE_WAIT         4U

#define USB2X_MINIPORT_RETURN_SUCCESS          0U
#define USB2X_MINIPORT_RETURN_INVALID_MESSAGE  1U
#define USB2X_MINIPORT_RETURN_STALE_GENERATION 2U
#define USB2X_MINIPORT_RETURN_NO_PENDING       3U
#define USB2X_MINIPORT_RETURN_BUSY             4U
#define USB2X_MINIPORT_RETURN_NOT_READY        5U

#define USB2X_DIRECTION_NONE               0U
#define USB2X_DIRECTION_IN                 1U
#define USB2X_DIRECTION_OUT                2U

#define USB2X_ADAPTER_OFFLINE              0U
#define USB2X_ADAPTER_ONLINE               1U

#define USB2X_TRANSPORT_SUCCESS            0U
#define USB2X_TRANSPORT_NO_DEVICE          1U
#define USB2X_TRANSPORT_TIMEOUT            2U
#define USB2X_TRANSPORT_CANCELLED          3U
#define USB2X_TRANSPORT_IO_ERROR           4U
#define USB2X_TRANSPORT_PROTOCOL_ERROR     5U
#define USB2X_TRANSPORT_BLOCKED            6U
#define USB2X_TRANSPORT_STALE_GENERATION   7U

#define USB2X_SRB_FUNCTION_EXECUTE_SCSI    0U

#define USB2X_MAX_CDB_LENGTH               16U
#define USB2X_MAX_SENSE_LENGTH             32U
#define USB2X_MAX_TRANSFER_LENGTH          4112U
#define USB2X_MAX_TIMEOUT_MS               60000U
#define USB2X_MAX_SRB_TIMEOUT_SECONDS      600U
#define USB2X_INQUIRY_LENGTH               36U

#pragma pack(push, 8)

typedef struct _USB2X_BROKER_HEADER {
    USB2X_U32 Magic;
    USB2X_U16 VersionMajor;
    USB2X_U16 VersionMinor;
    USB2X_U16 MessageType;
    USB2X_U16 HeaderSize;
    USB2X_U32 MessageSize;
    USB2X_U64 RequestId;
    USB2X_U64 Generation;
} USB2X_BROKER_HEADER;

typedef struct _USB2X_SCSI_REQUEST {
    USB2X_BROKER_HEADER Header;
    USB2X_U8 PathId;
    USB2X_U8 TargetId;
    USB2X_U8 Lun;
    USB2X_U8 PhysicalTargetId;
    USB2X_U8 PhysicalLun;
    USB2X_U8 CdbLength;
    USB2X_U8 Direction;
    USB2X_U8 SrbFunction;
    USB2X_U32 TransferLength;
    USB2X_U32 TimeoutMilliseconds;
    USB2X_U16 SenseAllocationLength;
    USB2X_U16 Reserved0;
    USB2X_U8 Cdb[USB2X_MAX_CDB_LENGTH];
    USB2X_U32 DataLength;
    USB2X_U8 Reserved1[8];
    USB2X_U8 Data[USB2X_MAX_TRANSFER_LENGTH];
} USB2X_SCSI_REQUEST;

typedef struct _USB2X_SCSI_COMPLETION {
    USB2X_BROKER_HEADER Header;
    USB2X_U32 TransportStatus;
    USB2X_U8 AdapterStatus;
    USB2X_U8 ScsiStatus;
    USB2X_U8 SenseLength;
    USB2X_U8 Reserved0;
    USB2X_U32 TransferLength;
    USB2X_U32 Residue;
    USB2X_U32 DataLength;
    USB2X_U32 Reserved1;
    USB2X_U8 Sense[USB2X_MAX_SENSE_LENGTH];
    USB2X_U8 Data[USB2X_MAX_TRANSFER_LENGTH];
} USB2X_SCSI_COMPLETION;

typedef struct _USB2X_ADAPTER_STATE {
    USB2X_BROKER_HEADER Header;
    USB2X_U32 State;
    USB2X_U8 PhysicalTargetId;
    USB2X_U8 PhysicalLun;
    USB2X_U8 PeripheralDeviceType;
    USB2X_U8 Reserved0;
    USB2X_U32 InquiryLength;
    USB2X_U8 Inquiry[USB2X_INQUIRY_LENGTH];
} USB2X_ADAPTER_STATE;

/*
 * Input to IOCTL_MINIPORT_PROCESS_SERVICE_IRP. The caller supplies the
 * generation it has synchronized with. On success the same METHOD_BUFFERED
 * system buffer is replaced with one USB2X_SCSI_REQUEST.
 */
typedef struct _USB2X_SERVICE_WAIT {
    USB2X_BROKER_HEADER Header;
} USB2X_SERVICE_WAIT;

#pragma pack(pop)

#define USB2X_BROKER_HEADER_SIZE           32U
#define USB2X_SCSI_REQUEST_PREFIX_SIZE     80U
#define USB2X_SCSI_REQUEST_MAX_SIZE        \
    (USB2X_SCSI_REQUEST_PREFIX_SIZE + USB2X_MAX_TRANSFER_LENGTH)
#define USB2X_SCSI_COMPLETION_PREFIX_SIZE  88U
#define USB2X_SCSI_COMPLETION_MAX_SIZE     4200U
#define USB2X_ADAPTER_STATE_SIZE           80U
#define USB2X_SERVICE_WAIT_SIZE             32U
