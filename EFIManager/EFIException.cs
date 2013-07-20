using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EFIManager
{
	class EFIException : Exception
	{
		public EFIException(String reason) : base(reason) { }

		public EFIException(String msg, params object[] format) : this(String.Format(msg, format)) { }

	}
}
