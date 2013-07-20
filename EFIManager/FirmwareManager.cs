using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace EFIManager
{
	public class FirmwareManager
	{

		#region DLLImports

		[DllImport("kernel32.dll", EntryPoint = "GetFirmwareEnvironmentVariableW", SetLastError = true)]
		public static extern uint GetFirmwareEnvironmentVariableW([MarshalAs(UnmanagedType.LPWStr)] string lpName, [MarshalAs(UnmanagedType.LPWStr)] string lpGuid, IntPtr pBuffer, uint nSize);

		#endregion


		private const string EFI_GLOBAL_VARIABLE = "{8BE4DF61-93CA-11D2-AA0D-00E098032B8C}";

		private Boolean debug = true;
		public Boolean Debug { get { return debug; } set { debug = value; } }

		public FirmwareManager()
		{
			if (!BitConverter.IsLittleEndian)
			{
				Console.WriteLine("Your system seems to not be Little Endian. Unsure what will happen here, good luck!");
			}

		}


		public UInt16[] GetBootOrder()
		{
			byte[] sb = GetEfiVar("BootOrder", EFI_GLOBAL_VARIABLE);
			UInt16[] ret = new UInt16[sb.Length / 2];

			for (int i = 0; i < ret.Length; i++)
			{
				ret[i] = BitConverter.ToUInt16(sb, i * 2);
			}
			return ret;
		}

		public LoadOption GetBootInfo(UInt16 bootNumber)
		{
			byte[] buf = GetEfiVar("Boot" + bootNumber.ToString("X4"), EFI_GLOBAL_VARIABLE);
			return new LoadOption(buf);
		}


		public byte[] GetEfiVar(string lpName, string lpGuid)
		{
			D("Reading {0}:{1}", lpName, lpGuid);
			byte[] buf = new byte[1024];
			IntPtr bufPtr = Marshal.AllocHGlobal(buf.Length);
			Marshal.Copy(buf, 0, bufPtr, buf.Length);

			uint size = GetFirmwareEnvironmentVariableW(lpName, lpGuid, bufPtr, (uint)buf.Length);

			Marshal.Copy(bufPtr, buf, 0, (int)size);

			Marshal.FreeHGlobal(bufPtr);

			if (Marshal.GetLastWin32Error() != 0)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}
			Array.Resize(ref buf, (int)size);

			D("Size: " + size);
			D("Ret: [{0}]", String.Join(",", buf));

			return buf;
		}


		private void D(String msg, params object[] args)
		{
			if (debug)
			{
				Console.WriteLine("D:" + msg, args);
			}
		}


		/// <summary>
		/// This code was adapted from http://ithoughthecamewithyou.com/post/Reboot-computer-in-C-NET.aspx
		/// </summary>


		public void UpgradePrivilage()
		{
			IntPtr tokenHandle = IntPtr.Zero;

			try
			{
				// get process token
				if (!OpenProcessToken(Process.GetCurrentProcess().Handle,
					TOKEN_QUERY | TOKEN_ADJUST_PRIVILEGES,
					out tokenHandle))
				{
					throw new Win32Exception(Marshal.GetLastWin32Error(),
						"Failed to open process token handle");
				}

				// lookup the shutdown privilege
				TOKEN_PRIVILEGES tokenPrivs = new TOKEN_PRIVILEGES();
				tokenPrivs.PrivilegeCount = 1;
				tokenPrivs.Privileges = new LUID_AND_ATTRIBUTES[1];
				tokenPrivs.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;

				if (!LookupPrivilegeValue(null,
					SE_SYSTEM_ENVIRONMENT_NAME,
					out tokenPrivs.Privileges[0].Luid))
				{
					throw new Win32Exception(Marshal.GetLastWin32Error(),
						"Failed to open lookup shutdown privilege");
				}


				// add the shutdown privilege to the process token
				if (!AdjustTokenPrivileges(tokenHandle,
					false,
					ref tokenPrivs,
					0,
					IntPtr.Zero,
					IntPtr.Zero))
				{
					throw new Win32Exception(Marshal.GetLastWin32Error(),
						"Failed to adjust process token privileges");
				}


				Console.WriteLine("Privilages should be OK!");

			}
			finally
			{
				// close the process token
				if (tokenHandle != IntPtr.Zero)
				{
					CloseHandle(tokenHandle);
				}
			}
		}




		#region Stuff for Privilage fixing
		[StructLayout(LayoutKind.Sequential)]
		private struct LUID
		{
			public uint LowPart;
			public int HighPart;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct LUID_AND_ATTRIBUTES
		{
			public LUID Luid;
			public UInt32 Attributes;
		}

		private struct TOKEN_PRIVILEGES
		{
			public UInt32 PrivilegeCount;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
			public LUID_AND_ATTRIBUTES[] Privileges;
		}


		private const UInt32 TOKEN_QUERY = 0x0008;
		private const UInt32 TOKEN_ADJUST_PRIVILEGES = 0x0020;
		private const UInt32 SE_PRIVILEGE_ENABLED = 0x00000002;
		// private const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
		private const string SE_SYSTEM_ENVIRONMENT_NAME = "SeSystemEnvironmentPrivilege";


		[DllImport("advapi32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool OpenProcessToken(IntPtr ProcessHandle,
			UInt32 DesiredAccess,
			out IntPtr TokenHandle);

		[DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool LookupPrivilegeValue(string lpSystemName,
			string lpName,
			out LUID lpLuid);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool CloseHandle(IntPtr hObject);

		[DllImport("advapi32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle,
			[MarshalAs(UnmanagedType.Bool)]bool DisableAllPrivileges,
			ref TOKEN_PRIVILEGES NewState,
			UInt32 Zero,
			IntPtr Null1,
			IntPtr Null2);

		[DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern bool LookupPrivilegeValue([MarshalAs(UnmanagedType.LPTStr)] string lpSystemName, [MarshalAs(UnmanagedType.LPTStr)] string lpName, out Int64 lpLuid);


		#endregion


	}
}
