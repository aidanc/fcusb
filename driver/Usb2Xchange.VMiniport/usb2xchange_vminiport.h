// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
#pragma once

#include <ntddk.h>
#include <storport.h>
#include <ntddscsi.h>

#include "..\shared\usb2xchange_broker_protocol.h"

#define USB2XCHANGE_MAX_TRANSFER_BYTES USB2X_MAX_TRANSFER_LENGTH
#define USB2XCHANGE_MAX_PHYSICAL_BREAKS 3UL
#define USB2XCHANGE_NUMBER_OF_BUSES 1U
#define USB2XCHANGE_MAX_TARGETS 2U
#define USB2XCHANGE_INITIATOR_ID 1
#define USB2XCHANGE_MAX_LUNS 1U
#define USB2XCHANGE_MAX_PHYSICAL_TARGET 6U
#define USB2XCHANGE_SCANNER_DEVICE_TYPE 6U

typedef struct _USB2XCHANGE_ADAPTER_EXTENSION {
    BOOLEAN Initialized;
    BOOLEAN Stopped;
    BOOLEAN Online;
    BOOLEAN PendingRequestPublished;
    USB2X_U64 Generation;
    USB2X_U64 NextRequestId;
    USB2X_U8 PhysicalTargetId;
    USB2X_U8 PhysicalLun;
    USB2X_U8 Reserved[6];
    USB2X_U8 Inquiry[USB2X_INQUIRY_LENGTH];
    PIRP ServiceIrp;
    PSCSI_REQUEST_BLOCK PendingSrb;
    USB2X_SCSI_REQUEST PendingRequest;
} USB2XCHANGE_ADAPTER_EXTENSION, *PUSB2XCHANGE_ADAPTER_EXTENSION;

DRIVER_INITIALIZE DriverEntry;

HW_INITIALIZE Usb2XchangeHwInitialize;
HW_STARTIO Usb2XchangeHwStartIo;
HW_RESET_BUS Usb2XchangeHwResetBus;
HW_ADAPTER_CONTROL Usb2XchangeHwAdapterControl;
HW_FREE_ADAPTER_RESOURCES Usb2XchangeHwFreeAdapterResources;
HW_PROCESS_SERVICE_REQUEST Usb2XchangeHwProcessServiceRequest;
HW_COMPLETE_SERVICE_IRP Usb2XchangeHwCompleteServiceIrp;
VIRTUAL_HW_FIND_ADAPTER Usb2XchangeVirtualHwFindAdapter;
