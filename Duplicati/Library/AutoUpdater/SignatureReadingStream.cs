﻿//  Copyright (C) 2014, Kenneth Skovhede

//  http://www.hexad.dk, opensource@hexad.dk
//
//  This library is free software; you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as
//  published by the Free Software Foundation; either version 2.1 of the
//  License, or (at your option) any later version.
//
//  This library is distributed in the hope that it will be useful, but
//  WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
//  Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public
//  License along with this library; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
using System;

namespace Duplicati.Library.AutoUpdater
{
    public class SignatureReadingStream : System.IO.Stream, IDisposable
    {
        /// <summary>
        /// The size of the SHA256 output hash in bytes
        /// </summary>
        /// 
        internal const int SIGNED_HASH_SIZE = 128;        

        /// <summary>
        /// The stream to read from
        /// </summary>
        private System.IO.Stream m_stream;

        protected SignatureReadingStream()
        {
        }

        public SignatureReadingStream(System.IO.Stream stream, System.Security.Cryptography.RSACryptoServiceProvider key)
        {
            if (!VerifySignature(stream, key))
                throw new System.IO.InvalidDataException("Unable to verify signature");
            m_stream = stream;
        }

        private static bool VerifySignature(System.IO.Stream stream, System.Security.Cryptography.RSACryptoServiceProvider key)
        {
            stream.Position = stream.Length - SIGNED_HASH_SIZE;
            var signature = new byte[SIGNED_HASH_SIZE];
            if (stream.Read(signature, 0, signature.Length) != signature.Length)
                throw new System.IO.InvalidDataException("Unexpected end-of-stream while reading signature");
            var sha256 = System.Security.Cryptography.SHA256.Create();
            sha256.Initialize();

            var bytes = stream.Length - (signature.Length);
            stream.Position = 0;
            var buf = new byte[8 * 1024];
            while (bytes > 0)
            {
                var r = stream.Read(buf, 0, (int)Math.Min(bytes, buf.Length));
                if (r == 0)
                    throw new Exception("Unexpected end-of-stream while reading content");
                bytes -= r;
                sha256.TransformBlock(buf, 0, r, buf, 0);
            }

            sha256.TransformFinalBlock(buf, 0, 0);
            var hash = sha256.Hash;
            var OID = System.Security.Cryptography.CryptoConfig.MapNameToOID("SHA256");
            return key.VerifyHash(hash, OID, signature);
        }

        public static void CreateSignedStream(System.IO.Stream datastream, System.IO.Stream signedstream, System.Security.Cryptography.RSACryptoServiceProvider key)
        {
            var sha256 = System.Security.Cryptography.SHA256.Create();

            datastream.Position = 0;
            signedstream.Position = SIGNED_HASH_SIZE;

            var buf = new byte[8 * 1024];
            var bytes = datastream.Length;
            while (bytes > 0)
            {
                var r = datastream.Read(buf, 0, (int)Math.Min(bytes, buf.Length));
                if (r == 0)
                    throw new Exception("Unexpected end-of-stream while reading content");

                signedstream.Write(buf, 0, r);

                bytes -= r;
                sha256.TransformBlock(buf, 0, r, buf, 0);
            }

            sha256.TransformFinalBlock(buf, 0, 0);
            var hash = sha256.Hash;

            var OID = System.Security.Cryptography.CryptoConfig.MapNameToOID("SHA256");
            var signature = key.SignHash(hash, OID);

            signedstream.Position = 0;
            signedstream.Write(signature, 0, signature.Length);
        }

        #region implemented abstract members of Stream

        public override void Flush()
        {
            try { m_stream.Flush(); }
            catch { }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            count = (int)Math.Min(count, this.Length - m_stream.Position);
            return m_stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            switch (origin)
            {
                case System.IO.SeekOrigin.Current:
                    return Seek(m_stream.Position + offset, System.IO.SeekOrigin.Begin);
                case System.IO.SeekOrigin.End:
                    return m_stream.Seek(offset + SIGNED_HASH_SIZE, origin);
                case System.IO.SeekOrigin.Begin:
                default:
                    return m_stream.Seek(Math.Min(offset, m_stream.Length - SIGNED_HASH_SIZE), origin);
            }
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                return m_stream.Length - SIGNED_HASH_SIZE;
            }
        }

        public override long Position
        {
            get
            {
                return m_stream.Position;
            }
            set
            {
                m_stream.Seek(value, System.IO.SeekOrigin.Begin);
            }
        }

        #endregion
    }
}
