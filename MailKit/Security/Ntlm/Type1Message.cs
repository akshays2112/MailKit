﻿//
// Mono.Security.Protocol.Ntlm.Type1Message - Negotiation
//
// Authors: Sebastien Pouliot <sebastien@ximian.com>
//          Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2003 Motus Technologies Inc. (http://www.motus.com)
// Copyright (c) 2004 Novell, Inc (http://www.novell.com)
// Copyright (c) 2013-2021 .NET Foundation and Contributors
//
// References
// a.	NTLM Authentication Scheme for HTTP, Ronald Tschalär
//	http://www.innovation.ch/java/ntlm.html
// b.	The NTLM Authentication Protocol, Copyright © 2003 Eric Glass
//	http://davenport.sourceforge.net/ntlm.html
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Text;

namespace MailKit.Security.Ntlm {
	class Type1Message : MessageBase
	{
		internal static readonly NtlmFlags DefaultFlags = NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateOem | NtlmFlags.NegotiateUnicode | NtlmFlags.RequestTarget;

		string workstation;
		string domain;

		public Type1Message (string workstation, string domainName, Version osVersion) : base (1)
		{
			Flags = DefaultFlags;
			Workstation = workstation;
			OSVersion = osVersion;
			Domain = domainName;

			if (osVersion != null)
				Flags |= NtlmFlags.NegotiateVersion;
		}

		public Type1Message (byte[] message, int startIndex, int length) : base (1)
		{
			Decode (message, startIndex, length);
		}

		public string Domain {
			get { return domain; }
			set {
				if (string.IsNullOrEmpty (value)) {
					Flags &= ~NtlmFlags.NegotiateDomainSupplied;
					value = string.Empty;
				} else {
					Flags |= NtlmFlags.NegotiateDomainSupplied;
				}

				domain = value;
			}
		}

		public string Workstation {
			get { return workstation; }
			set {
				if (string.IsNullOrEmpty (value)) {
					Flags &= ~NtlmFlags.NegotiateWorkstationSupplied;
					value = string.Empty;
				} else {
					Flags |= NtlmFlags.NegotiateWorkstationSupplied;
				}

				workstation = value;
			}
		}

		void Decode (byte[] message, int startIndex, int length)
		{
			ValidateArguments (message, startIndex, length);

			Flags = (NtlmFlags) BitConverterLE.ToUInt32 (message, startIndex + 12);

			// decode the domain
			var domainLength = BitConverterLE.ToUInt16 (message, startIndex + 16);
			var domainOffset = BitConverterLE.ToUInt16 (message, startIndex + 20);
			domain = Encoding.UTF8.GetString (message, startIndex + domainOffset, domainLength);

			// decode the workstation/host
			var workstationLength = BitConverterLE.ToUInt16 (message, startIndex + 24);
			var workstationOffset = BitConverterLE.ToUInt16 (message, startIndex + 28);
			workstation = Encoding.UTF8.GetString (message, startIndex + workstationOffset, workstationLength);

			if ((Flags & NtlmFlags.NegotiateVersion) != 0 && length >= 40) {
				// decode the OS Version
				int major = message[startIndex + 32];
				int minor = message[startIndex + 33];
				int build = BitConverterLE.ToUInt16 (message, startIndex + 34);

				OSVersion = new Version (major, minor, build);
			}
		}

		public override byte[] Encode ()
		{
			bool negotiateVersion;
			int versionLength = 0;

			if (negotiateVersion = (Flags & NtlmFlags.NegotiateVersion) != 0)
				versionLength = 8;

			int workstationOffset = 32 + versionLength;
			int domainOffset = workstationOffset + workstation.Length;

			var message = PrepareMessage (32 + domain.Length + workstation.Length + versionLength);
			byte[] buffer;

			message[12] = (byte) Flags;
			message[13] = (byte)((uint) Flags >> 8);
			message[14] = (byte)((uint) Flags >> 16);
			message[15] = (byte)((uint) Flags >> 24);

			message[16] = (byte) domain.Length;
			message[17] = (byte)(domain.Length >> 8);
			message[18] = message[16];
			message[19] = message[17];
			message[20] = (byte) domainOffset;
			message[21] = (byte)(domainOffset >> 8);

			message[24] = (byte) workstation.Length;
			message[25] = (byte)(workstation.Length >> 8);
			message[26] = message[24];
			message[27] = message[25];
			message[28] = (byte) workstationOffset;
			message[29] = (byte)(workstationOffset >> 8);

			if (negotiateVersion) {
				message[32] = (byte) OSVersion.Major;
				message[33] = (byte) OSVersion.Minor;
				message[34] = (byte) OSVersion.Build;
				message[35] = (byte)(OSVersion.Build >> 8);
				message[36] = 0x00;
				message[37] = 0x00;
				message[38] = 0x00;
				message[39] = 0x0f;
			}

			buffer = Encoding.UTF8.GetBytes (workstation.ToUpperInvariant ());
			Buffer.BlockCopy (buffer, 0, message, workstationOffset, buffer.Length);

			buffer = Encoding.UTF8.GetBytes (domain.ToUpperInvariant ());
			Buffer.BlockCopy (buffer, 0, message, domainOffset, buffer.Length);

			return message;
		}
	}
}
