using System.Runtime.InteropServices;

namespace Steamworks;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct InputDigitalActionData_t
{
	[MarshalAs(UnmanagedType.I1)]
	public bool bState;

	[MarshalAs(UnmanagedType.I1)]
	public bool bActive;
}
