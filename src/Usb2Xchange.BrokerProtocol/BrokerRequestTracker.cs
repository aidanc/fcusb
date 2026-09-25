// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;

namespace Usb2Xchange.BrokerProtocol
{
    public enum BrokerSrbStatus : byte
    {
        Success = 0x01,
        Aborted = 0x02,
        Error = 0x04,
        Busy = 0x05,
        InvalidRequest = 0x06,
        NoDevice = 0x08,
        Timeout = 0x09,
        SelectionTimeout = 0x0A,
        RequestFlushed = 0x16
    }

    public sealed class BrokerSrbResult
    {
        private readonly byte[] sense;
        private readonly byte[] data;

        internal BrokerSrbResult(BrokerSrbStatus srbStatus, byte scsiStatus,
            uint transferLength, byte[] sense, byte[] data)
        {
            SrbStatus = srbStatus;
            ScsiStatus = scsiStatus;
            TransferLength = transferLength;
            this.sense = (byte[])sense.Clone();
            this.data = (byte[])data.Clone();
        }

        public BrokerSrbStatus SrbStatus { get; private set; }
        public byte ScsiStatus { get; private set; }
        public uint TransferLength { get; private set; }
        public byte[] Sense { get { return (byte[])sense.Clone(); } }
        public byte[] Data { get { return (byte[])data.Clone(); } }
    }

    public static class BrokerSrbMapper
    {
        public static BrokerSrbResult Map(BrokerRequest request,
            BrokerCompletion completion)
        {
            BrokerWireProtocol.ValidateCompletion(request, completion);
            if (completion.TransportStatus != BrokerTransportStatus.Success)
            {
                return Empty(MapTransportStatus(completion.TransportStatus));
            }
            switch (completion.AdapterStatus)
            {
                case 0x00:
                    return new BrokerSrbResult(BrokerSrbStatus.Success,
                        completion.ScsiStatus, completion.TransferLength,
                        completion.Sense, completion.Data);
                case 0x02:
                    return new BrokerSrbResult(BrokerSrbStatus.Error,
                        completion.ScsiStatus, completion.TransferLength,
                        completion.Sense, completion.Data);
                case 0x08:
                    return Empty(BrokerSrbStatus.Busy);
                case 0x8A:
                    return Empty(BrokerSrbStatus.SelectionTimeout);
                default:
                    throw new BrokerProtocolException(
                        "Unknown adapter status in completion.");
            }
        }

        internal static BrokerSrbResult Empty(BrokerSrbStatus status)
        {
            return new BrokerSrbResult(status, 0, 0, new byte[0],
                new byte[0]);
        }

        private static BrokerSrbStatus MapTransportStatus(
            BrokerTransportStatus status)
        {
            switch (status)
            {
                case BrokerTransportStatus.NoDevice:
                    return BrokerSrbStatus.NoDevice;
                case BrokerTransportStatus.Timeout:
                    return BrokerSrbStatus.Timeout;
                case BrokerTransportStatus.Cancelled:
                    return BrokerSrbStatus.Aborted;
                case BrokerTransportStatus.Blocked:
                    return BrokerSrbStatus.InvalidRequest;
                case BrokerTransportStatus.StaleGeneration:
                    return BrokerSrbStatus.RequestFlushed;
                case BrokerTransportStatus.IoError:
                case BrokerTransportStatus.ProtocolError:
                    return BrokerSrbStatus.Error;
                default:
                    throw new BrokerProtocolException(
                        "Transport success requires adapter status mapping.");
            }
        }
    }

    public sealed class BrokerRequestTracker
    {
        private readonly object sync = new object();
        private ulong generation;
        private ulong nextRequestId = 1;
        private bool online;
        private byte physicalTargetId;
        private byte physicalLun;
        private BrokerRequest pending;
        private bool dispatched;
        private ulong deadlineMilliseconds;

        public bool IsOnline
        {
            get { lock (sync) { return online; } }
        }

        public bool HasPendingRequest
        {
            get { lock (sync) { return pending != null; } }
        }

        public ulong Generation
        {
            get { lock (sync) { return generation; } }
        }

        public BrokerSrbResult ApplyAdapterState(BrokerAdapterState state)
        {
            BrokerWireProtocol.SerializeAdapterState(state);
            lock (sync)
            {
                if (state.Generation <= generation)
                {
                    throw new BrokerProtocolException(
                        "Adapter generation did not advance.");
                }
                BrokerSrbResult flushed = pending == null ? null :
                    BrokerSrbMapper.Empty(BrokerSrbStatus.RequestFlushed);
                pending = null;
                dispatched = false;
                deadlineMilliseconds = 0;
                generation = state.Generation;
                online = state.State == BrokerAdapterStateKind.Online;
                physicalTargetId = state.PhysicalTargetId;
                physicalLun = state.PhysicalLun;
                return flushed;
            }
        }

        public BrokerRequest Queue(byte pathId, byte targetId, byte lun,
            BrokerDirection direction, uint transferLength,
            uint timeoutMilliseconds, ushort senseAllocationLength, byte[] cdb,
            ulong nowMilliseconds)
        {
            lock (sync)
            {
                if (!online)
                {
                    throw new InvalidOperationException(
                        "The scanner target is offline.");
                }
                if (pending != null)
                {
                    throw new InvalidOperationException(
                        "Only one request may be outstanding.");
                }
                if (nextRequestId == 0)
                {
                    throw new InvalidOperationException(
                        "The request ID space is exhausted.");
                }
                BrokerRequest request = new BrokerRequest(nextRequestId,
                    generation, pathId, targetId, lun, physicalTargetId,
                    physicalLun, direction, transferLength,
                    timeoutMilliseconds, senseAllocationLength, cdb);
                BrokerWireProtocol.SerializeRequest(request);
                pending = request;
                dispatched = false;
                deadlineMilliseconds = nowMilliseconds >
                    ulong.MaxValue - timeoutMilliseconds
                    ? ulong.MaxValue
                    : nowMilliseconds + timeoutMilliseconds;
                nextRequestId = unchecked(nextRequestId + 1);
                return request;
            }
        }

        public BrokerRequest Dispatch()
        {
            lock (sync)
            {
                if (pending == null)
                {
                    throw new InvalidOperationException(
                        "There is no pending request.");
                }
                if (dispatched)
                {
                    throw new InvalidOperationException(
                        "The pending request was already dispatched.");
                }
                dispatched = true;
                return pending;
            }
        }

        public BrokerSrbResult Complete(BrokerCompletion completion)
        {
            lock (sync)
            {
                if (pending == null || !dispatched)
                {
                    throw new InvalidOperationException(
                        "There is no dispatched request to complete.");
                }
                BrokerSrbResult result = BrokerSrbMapper.Map(pending,
                    completion);
                pending = null;
                dispatched = false;
                deadlineMilliseconds = 0;
                return result;
            }
        }

        public BrokerSrbResult Cancel(ulong requestId)
        {
            lock (sync)
            {
                if (pending == null || pending.RequestId != requestId)
                {
                    throw new InvalidOperationException(
                        "The request is no longer pending.");
                }
                pending = null;
                dispatched = false;
                deadlineMilliseconds = 0;
                return BrokerSrbMapper.Empty(BrokerSrbStatus.Aborted);
            }
        }

        public BrokerSrbResult Expire(ulong nowMilliseconds)
        {
            lock (sync)
            {
                if (pending == null || nowMilliseconds < deadlineMilliseconds)
                {
                    return null;
                }
                pending = null;
                dispatched = false;
                deadlineMilliseconds = 0;
                return BrokerSrbMapper.Empty(BrokerSrbStatus.Timeout);
            }
        }
    }
}
