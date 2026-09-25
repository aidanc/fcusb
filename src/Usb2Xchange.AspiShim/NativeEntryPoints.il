// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
.assembly extern mscorlib
{
  .ver 4:0:0:0
}
.assembly extern Usb2Xchange.AspiShim.Managed
{
  .ver 0:0:0:0
}
.assembly wnaspi32
{
  .ver 1:0:0:0
}
.module wnaspi32.dll
.corflags 0x00000002

.method public static uint32 modopt([mscorlib]System.Runtime.CompilerServices.CallConvCdecl)
        GetASPI32SupportInfo() cil managed
{
  .vtentry 1 : 1
  .export [1] as GetASPI32SupportInfo
  .maxstack 1
  call uint32 [Usb2Xchange.AspiShim.Managed]Usb2Xchange.AspiShim.AspiExports::GetASPI32SupportInfo()
  ret
}

.method public static uint32 modopt([mscorlib]System.Runtime.CompilerServices.CallConvCdecl)
        SendASPI32Command(native int srb) cil managed
{
  .vtentry 1 : 2
  .export [2] as SendASPI32Command
  .maxstack 1
  ldarg.0
  call uint32 [Usb2Xchange.AspiShim.Managed]Usb2Xchange.AspiShim.AspiExports::SendASPI32Command(native int)
  ret
}

.data VTableFixups = bytearray (00 00 00 00 00 00 00 00)
.vtfixup [2] int32 fromunmanaged at VTableFixups
