using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EFIManager
{
	public class LoadOption
	{

		public const uint LOAD_OPTION_ACTIVE = 0x00000001;
		public const uint LOAD_OPTION_FORCE_RECONNECT = 0x00000002;
		public const uint LOAD_OPTION_HIDDEN = 0x00000008;
		public const uint LOAD_OPTION_CATEGORY = 0x00001F00;
		public const uint LOAD_OPTION_CATEGORY_BOOT = 0x00000000;
		public const uint LOAD_OPTION_CATEGORY_APP = 0x00000100;



		private byte[] buf;

		//UINT32 Attributes;
		//UINT16 FilePathListLength;
		//CHAR16 Description[];Unified Extensible Firmware Interface Specification
		//EFI_DEVICE_PATH_PROTOCOL FilePathList[];
		//UINT8 OptionalData[];


		private UInt32? attributes = null;
		private UInt16? filePathListLength = null;
		private String description = null;
		private DevicePath[] filePathList = null;
		private byte[] optionalData = null;


		public LoadOption(byte[] buf)
		{
			this.buf = buf;
		}

		public ushort FilePathListLength
		{
			get
			{
				return filePathListLength ?? (ushort)(filePathListLength = BitConverter.ToUInt16(buf, 4));
			}
		}



		public String Description
		{
			get
			{

				if (description == null)
				{
					StringBuilder sb = new StringBuilder();
					for (int i = 6; i < buf.Length; i += 2)
					{
						char c = (char)BitConverter.ToUInt32(buf, i);
						if (c == 0)
						{
							break;
						}
						sb.Append(c);
					}
					description = sb.ToString();
				}
				return description;
			}
			set { }
		}

		public DevicePath[] FilePathLists
		{
			get
			{
				filePathList = new DevicePath[0];
				int offset = 6 + Description.Length * 2 + 2;
				for (int i = 0; i < FilePathListLength; )
				{
					DevicePath dp = DevicePath.GetDevice(buf, offset + i);
					i += dp.Length;
					Array.Resize(ref filePathList, filePathList.Length + 1);
					filePathList[filePathList.Length - 1] = dp;
				}

				return filePathList;
			}
		}

		public byte[] OptionalData
		{
			get
			{
				if (optionalData == null)
				{
					int start = 4 + 2 + Description.Length + FilePathListLength;
					int len = buf.Length - start;
					optionalData = new byte[len];
					Array.Copy(buf, start, optionalData, 0, len);
				}
				return optionalData;
			}
		}


	}


	#region DevicePath Types

	public class DevicePath
	{
		protected byte[] buf;
		protected int offset;

		protected DevicePath(byte[] buf, int offset)
		{
			this.buf = buf;
			this.offset = offset;
		}

		public DevicePath(DevicePath dp) : this(dp.buf, dp.offset) { }

		public static DevicePath GetDevice(byte[] buf, int offset)
		{
			DevicePath device = new DevicePath(buf, offset);

			DevicePathAttribute dpa = new DevicePathAttribute()
			{
				Type = device.Type,
				SubType = device.SubType
			};

			if (DevicePathTypes.ContainsKey(dpa))
			{
				ConstructorInfo c = DevicePathTypes[dpa].GetConstructor(new Type[] { typeof(DevicePath) });

				device = (c.Invoke(new object[] { device }) as DevicePath) ?? device;
			}

			return device;
		}

		public override string ToString()
		{
			return String.Format("Base DevicePath: {{type: 0x{0}, sub-type: 0x{1}, length: 0x{2}}}", Type.ToString("X2"), SubType.ToString("X2"), Length.ToString("X4"));
		}




		public byte Type
		{
			get
			{
				return buf[offset];
			}
		}

		public byte SubType
		{
			get
			{
				return buf[offset + 1];
			}
		}

		public ushort Length
		{
			get
			{
				return BitConverter.ToUInt16(buf, offset + 2);
			}
		}

		private static Dictionary<DevicePathAttribute, Type> devicePathTypes = null;
		private static Dictionary<DevicePathAttribute, Type> DevicePathTypes
		{
			get
			{
				if (devicePathTypes == null)
				{
					devicePathTypes = new Dictionary<DevicePathAttribute, Type>();
					Assembly assembly = Assembly.GetExecutingAssembly();

					foreach (Type type in assembly.GetTypes())
					{
						object[] attribs;
						if ((attribs = type.GetCustomAttributes(typeof(DevicePathAttribute), true)).Length > 0)
						{
							foreach (object attrib in attribs)
							{
								DevicePathAttribute dpa = (DevicePathAttribute)attrib;
								devicePathTypes.Add(dpa, type);
							}
						}
					}


				}


				return devicePathTypes;
			}
		}

	}

	[DevicePath(Type = 0x04, SubType = 0x04)]
	public class FilePathMediaDevicePath : DevicePath
	{

		public FilePathMediaDevicePath(DevicePath dp) : base(dp) { }

		private string pathName = null;
		public String PathName
		{
			get
			{
				return pathName ?? (pathName = Encoding.Unicode.GetString(buf, offset + 4, Length - 6));
			}
		}

		public override string ToString()
		{
			return String.Format("FilePathMedia DevicePath: {{type: 0x{0}, sub-type: 0x{1}, PathName:{2}}}", Type.ToString("X2"), SubType.ToString("X2"), PathName);
		}


	}

	[DevicePath(Type = 0x04, SubType = 0x01)]
	public class HardDriveMediaDevicePath : DevicePath
	{
		public HardDriveMediaDevicePath(DevicePath dp) : base(dp) { }

		public uint PartitionNumber
		{
			get
			{
				return BitConverter.ToUInt32(buf, offset + 4);
			}
		}

		public ulong PartitionStart
		{
			get
			{
				return BitConverter.ToUInt64(buf, offset + 8);
			}
		}

		public ulong PartitionSize
		{
			get
			{
				return BitConverter.ToUInt64(buf, offset + 16);
			}
		}

		public byte[] PartitionSignature
		{
			get
			{
				byte[] ret = new byte[16];
				Array.Copy(buf, offset + 24, ret, 0, 16);
				return ret;
			}
		}

		public byte PartitionFormat
		{
			get
			{
				return buf[offset + 40];
			}
		}
		public byte SignatureType
		{
			get
			{
				return buf[offset + 41];
			}
		}


		public override string ToString()
		{
			return String.Format("HardDriveMedia DevicePath: {{type: 0x{0}, sub-type: 0x{1}, PartitionNumber: {2}, PartitionStart: {3}, PartitionSize: {4}, PartitionFormat: 0x{5}, SignatureType: 0x{6} }}",
				Type.ToString("X2"), SubType.ToString("X2"), PartitionNumber, PartitionStart, PartitionSize, PartitionFormat.ToString("X2"), SignatureType.ToString("X2"));
		}



	}

	[DevicePath(Type = 0x7F, SubType = 0x01)]
	[DevicePath(Type = 0x7F, SubType = 0xFF)]
	public class EndOfHardwareDevicePath : DevicePath
	{
		public EndOfHardwareDevicePath(DevicePath dp) : base(dp) { }

		public override string ToString()
		{
			return String.Format("EndOfHardware DevicePath: {{{0}, SubType: 0x{1}}}", SubType == 0x01 ? "End This Instance of a Device Path " : "End Entire Device Path", SubType.ToString("X2"));
		}
	}

	[DevicePath(Type = 0x05, SubType = 0x01)]
	public class BIOSBootSpecificationDevicePath : DevicePath
	{
		public BIOSBootSpecificationDevicePath(DevicePath dp) : base(dp) { }

		public ushort DeviceType
		{
			get
			{
				return BitConverter.ToUInt16(buf, offset + 4);
			}
		}

		public ushort StatusFlag
		{
			get
			{
				return BitConverter.ToUInt16(buf, offset + 6);
			}
		}

		private string description = null;
		public string Description
		{
			get
			{
				if (description == null)
				{
					if (Length > 10)
					{
						description = Encoding.Unicode.GetString(buf, offset + 8, Length - 10);
					}
					description = "";
				}
				return description;
			}
		}

		public override string ToString()
		{
			return String.Format("BIOSBootSpecification DevicePath: {{DeviceType: 0x{0}, StatusFlag: 0x{1}, Description: {2}}}", DeviceType.ToString("X4"), StatusFlag.ToString("X4"), Description);
		}
	}


	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class DevicePathAttribute : Attribute
	{
		public int Type { get; set; }
		public int SubType { get; set; }

		public override bool Equals(object o)
		{
			if (o == null)
			{
				return false;
			}
			if (o is DevicePathAttribute)
			{
				DevicePathAttribute dpa = o as DevicePathAttribute;
				return dpa.Type == Type && dpa.SubType == SubType;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return Type.GetHashCode() ^ SubType.GetHashCode();
		}



	}

	#endregion
}
