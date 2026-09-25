// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
#include "..\..\driver\shared\usb2xchange_broker_protocol.h"

#define USB2X_OFFSET_OF(type, field) \
    ((unsigned __int64)&(((type *)0)->field))
#define USB2X_STATIC_ASSERT(name, expression) \
    typedef char name[(expression) ? 1 : -1]

USB2X_STATIC_ASSERT(assert_u8_size, sizeof(USB2X_U8) == 1);
USB2X_STATIC_ASSERT(assert_u16_size, sizeof(USB2X_U16) == 2);
USB2X_STATIC_ASSERT(assert_u32_size, sizeof(USB2X_U32) == 4);
USB2X_STATIC_ASSERT(assert_u64_size, sizeof(USB2X_U64) == 8);
USB2X_STATIC_ASSERT(assert_srb_signature_size,
    sizeof(USB2X_SRB_IO_CONTROL_SIGNATURE) == 8);

USB2X_STATIC_ASSERT(assert_header_size,
    sizeof(USB2X_BROKER_HEADER) == USB2X_BROKER_HEADER_SIZE);
USB2X_STATIC_ASSERT(assert_header_request_id,
    USB2X_OFFSET_OF(USB2X_BROKER_HEADER, RequestId) == 16);
USB2X_STATIC_ASSERT(assert_header_generation,
    USB2X_OFFSET_OF(USB2X_BROKER_HEADER, Generation) == 24);

USB2X_STATIC_ASSERT(assert_request_size,
    sizeof(USB2X_SCSI_REQUEST) == USB2X_SCSI_REQUEST_MAX_SIZE);
USB2X_STATIC_ASSERT(assert_request_bounds,
    USB2X_SCSI_REQUEST_MAX_SIZE == USB2X_SCSI_REQUEST_PREFIX_SIZE +
        USB2X_MAX_TRANSFER_LENGTH);
USB2X_STATIC_ASSERT(assert_request_transfer_length,
    USB2X_OFFSET_OF(USB2X_SCSI_REQUEST, TransferLength) == 40);
USB2X_STATIC_ASSERT(assert_request_cdb,
    USB2X_OFFSET_OF(USB2X_SCSI_REQUEST, Cdb) == 52);
USB2X_STATIC_ASSERT(assert_request_data_length,
    USB2X_OFFSET_OF(USB2X_SCSI_REQUEST, DataLength) == 68);
USB2X_STATIC_ASSERT(assert_request_data,
    USB2X_OFFSET_OF(USB2X_SCSI_REQUEST, Data) ==
        USB2X_SCSI_REQUEST_PREFIX_SIZE);

USB2X_STATIC_ASSERT(assert_completion_size,
    sizeof(USB2X_SCSI_COMPLETION) == USB2X_SCSI_COMPLETION_MAX_SIZE);
USB2X_STATIC_ASSERT(assert_completion_transfer_length,
    USB2X_OFFSET_OF(USB2X_SCSI_COMPLETION, TransferLength) == 40);
USB2X_STATIC_ASSERT(assert_completion_sense,
    USB2X_OFFSET_OF(USB2X_SCSI_COMPLETION, Sense) == 56);
USB2X_STATIC_ASSERT(assert_completion_data,
    USB2X_OFFSET_OF(USB2X_SCSI_COMPLETION, Data) ==
        USB2X_SCSI_COMPLETION_PREFIX_SIZE);

USB2X_STATIC_ASSERT(assert_adapter_state_size,
    sizeof(USB2X_ADAPTER_STATE) == USB2X_ADAPTER_STATE_SIZE);
USB2X_STATIC_ASSERT(assert_adapter_state_inquiry,
    USB2X_OFFSET_OF(USB2X_ADAPTER_STATE, Inquiry) == 44);

USB2X_STATIC_ASSERT(assert_service_wait_size,
    sizeof(USB2X_SERVICE_WAIT) == USB2X_SERVICE_WAIT_SIZE);
