using System.Runtime.InteropServices;

namespace Steamworks;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct InputAnalogActionData_t
{
	public EInputSourceMode eMode;

	public float x;

	public float y;

	[MarshalAs(UnmanagedType.I1)]
	public bool bActive;
}
