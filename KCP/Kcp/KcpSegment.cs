﻿using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using YYHEggEgg.Logger;

namespace System.Net.Sockets.Kcp
{
    /// <summary>
    /// 调整了没存布局，直接拷贝块提升性能。
    /// <para>结构体保存内容只有一个指针，不用担心参数传递过程中的性能</para>
    /// https://github.com/skywind3000/kcp/issues/118#issuecomment-338133930
    /// <para>不要对没有初始化的KcpSegment(内部指针为0，所有属性都将指向位置区域) 进行任何赋值操作，可能导致内存损坏。
    /// 出于性能考虑，没有对此项进行安全检查。</para>
    /// </summary>
    public struct KcpSegment : IKcpSegment
    {
#if false
        private string Invoker()
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame frame = stackTrace.GetFrame(2);
            return frame.ToString() ?? "-----Invoker Not found-----";
        }
#endif

#if BUGFIX_TRIAL_20230611_001
        private static object marshal_lck = new object();
#endif
        internal readonly unsafe byte* ptr;
        public unsafe KcpSegment(byte* intPtr, uint appendDateSize)
        {
            this.ptr = intPtr;
            len = appendDateSize;
        }

        /// <summary>
        /// 使用完必须显示释放，否则内存泄漏
        /// </summary>
        /// <param name="appendDateSize"></param>
        /// <returns></returns>
        public static KcpSegment AllocHGlobal(int appendDateSize)
        {
            var total = LocalOffset + HeadOffset + appendDateSize;
#if BUGFIX_TRIAL_20230611_001
            IntPtr intPtr;
            lock (marshal_lck)
            {
                intPtr = Marshal.AllocHGlobal(total);
            }
#else
            IntPtr intPtr = Marshal.AllocHGlobal(total);
#endif
#if false
            Log.Verb($"KcpSegment memory alloc: 0x{intPtr:x}", nameof(KcpSegment));
#endif
            unsafe
            {
                ///清零    不知道是不是有更快的清0方法？
                Span<byte> span = new Span<byte>(intPtr.ToPointer(), total);
                span.Clear();

                return new KcpSegment((byte*)intPtr.ToPointer(), (uint)appendDateSize);
            }
        }

        /// <summary>
        /// 释放非托管内存
        /// </summary>
        /// <param name="seg"></param>
        public static void FreeHGlobal(KcpSegment seg)
        {
            unsafe
            {
#if BUGFIX_TRIAL_20230611_001
                lock (marshal_lck)
                {
#endif
                    Marshal.FreeHGlobal((IntPtr)seg.ptr);
#if BUGFIX_TRIAL_20230611_001
                }
#endif
#if false
                Log.Verb($"KcpSegment memory free: 0x{(IntPtr)seg.ptr:x}", nameof(KcpSegment));
#endif
            }
        }

        /// 以下为本机使用的参数
        /// <summary>
        /// offset = 0
        /// </summary>
        public uint resendts
        {
            get
            {
                unsafe
                {
                    return *(uint*)(ptr + 0);
                }
            }
            set
            {
                unsafe
                {
                    *(uint*)(ptr + 0) = value;
                }
            }
        }

        /// <summary>
        /// offset = 4
        /// </summary>
        public uint rto
        {
            get
            {
                unsafe
                {
                    return *(uint*)(ptr + 4);
                }
            }
            set
            {
                unsafe
                {
                    *(uint*)(ptr + 4) = value;
                }
            }
        }

        /// <summary>
        /// offset = 8
        /// </summary>
        public uint fastack
        {
            get
            {
                unsafe
                {
                    return *(uint*)(ptr + 8);
                }
            }
            set
            {
                unsafe
                {
                    *(uint*)(ptr + 8) = value;
                }
            }
        }

        /// <summary>
        /// offset = 12
        /// </summary>
        public uint xmit
        {
            get
            {
                unsafe
                {
                    return *(uint*)(ptr + 12);
                }
            }
            set
            {
                unsafe
                {
                    *(uint*)(ptr + 12) = value;
                }
            }
        }

#if BYTE_CHECK_MODE

        /// <summary>
        /// offset = 16
        /// </summary>
        public uint byteCheckMode
        {
            get
            {
                unsafe
                {
                    return *(uint*)(ptr + 16);
                }
            }
            set
            {
                unsafe
                {
                    *(uint*)(ptr + 16) = value;
                }
            }
        }
#endif

        ///以下为需要网络传输的参数
#if BYTE_CHECK_MODE
        public const int LocalOffset = 4 * 5;
#else
        public const int LocalOffset = 4 * 4;
#endif
        public const int HeadOffset = KcpConst.IKCP_OVERHEAD;

        /// <summary>
        /// offset = <see cref="LocalOffset"/>
        /// </summary>
        /// https://github.com/skywind3000/kcp/issues/134
        public uint conv
        {
            get
            {
                unsafe
                {
                    return *(uint*)(LocalOffset + 0 + ptr);
                }
            }
            set
            {
                unsafe
                {
                    *(uint*)(LocalOffset + 0 + ptr) = value;
                }
            }
        }

#if MIHOMO_KCP
        /// <summary>
        /// miHoMo KCP modify: IUINT32 token
        /// <para/>Change line(s) in file compare: ikcp.h, +271
        /// <para/>offset = <see cref="LocalOffset"/> + 4
        /// </summary>
        public uint token
        {
            get
            {
                unsafe
                {
                    return *(uint*)(LocalOffset + 4 + ptr);
                }
            }
            set
            {
                unsafe
                {
                    *(uint*)(LocalOffset + 4 + ptr) = value;
                }
            }
        }
#endif

        // miHoMo KCP modify: IUINT32 token
        // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
        /// <summary>
        /// offset = <see cref="LocalOffset"/> + 4
        /// </summary>
#else
        /// <summary>
        /// offset = <see cref="LocalOffset"/> + 8
        /// </summary>
#endif
        public byte cmd
        {
            get
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    return *(LocalOffset + 4 + ptr);
#else
                    return *(LocalOffset + 8 + ptr);
#endif
                }
            }
            set
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    *(LocalOffset + 4 + ptr) = value;
#else
                    *(LocalOffset + 8 + ptr) = value;
#endif
                }
            }
        }

        // miHoMo KCP modify: IUINT32 token
        // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
        /// <summary>
        /// offset = <see cref="LocalOffset"/> + 5
        /// </summary>
#else
        /// <summary>
        /// offset = <see cref="LocalOffset"/> + 9
        /// </summary>
#endif
        public byte frg
        {
            get
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    return *(LocalOffset + 5 + ptr);
#else
                    return *(LocalOffset + 9 + ptr);
#endif
                }
            }
            set
            {
#if false
                Log.Verb($"Invoker: [{Invoker()}] assigned frg:{value} for KcpSegment: {this.ToLogString(false)}", nameof(KcpSegment));
#endif
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    *(LocalOffset + 5 + ptr) = value;
#else
                    *(LocalOffset + 9 + ptr) = value;
#endif
                }
            }
        }

        // miHoMo KCP modify: IUINT32 token
        // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
        /// <summary>
        /// offset = <see cref="LocalOffset"/> + 6
        /// </summary>
#else
        /// <summary>
        /// offset = <see cref="LocalOffset"/> + 10
        /// </summary>
#endif
        public ushort wnd
        {
            get
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    return *(ushort*)(LocalOffset + 6 + ptr);
#else
                    return *(ushort*)(LocalOffset + 10 + ptr);
#endif
                }
            }
            set
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    *(ushort*)(LocalOffset + 6 + ptr) = value;
#else
                    *(ushort*)(LocalOffset + 10 + ptr) = value;
#endif
                }
            }
        }

        // miHoMo KCP modify: IUINT32 token
        // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
        /// <summary>
        /// offset = <see cref="LocalOffset"/> + 8
        /// </summary>
#else
        /// <summary>
        /// offset = <see cref="LocalOffset"/> + 12
        /// </summary>
#endif
        public uint ts
        {
            get
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    return *(uint*)(LocalOffset + 8 + ptr);
#else
                    return *(uint*)(LocalOffset + 12 + ptr);
#endif
                }
            }
            set
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    *(uint*)(LocalOffset + 8 + ptr) = value;
#else
                    *(uint*)(LocalOffset + 12 + ptr) = value;
#endif
                }
            }
        }

        // miHoMo KCP modify: IUINT32 token
        // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
        /// <summary>
        /// <para> SendNumber? </para>
        /// offset = <see cref="LocalOffset"/> + 12
        /// </summary>
#else
        /// <summary>
        /// offset = <see cref="LocalOffset"/> + 16
        /// </summary>
#endif
        public uint sn
        {
            get
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    return *(uint*)(LocalOffset + 12 + ptr);
#else
                    return *(uint*)(LocalOffset + 16 + ptr);
#endif
                }
            }
            set
            {
#if false
                Log.Verb($"Invoker: [{Invoker()}] assigned sn:{value} for KcpSegment: {this.ToLogString(false)}", nameof(KcpSegment));
#endif
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    *(uint*)(LocalOffset + 12 + ptr) = value;
#else
                    *(uint*)(LocalOffset + 16 + ptr) = value;
#endif
                }
            }
        }

        // miHoMo KCP modify: IUINT32 token
        // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
        /// <summary>
        /// offset = <see cref="LocalOffset"/> + 16
        /// </summary>
#else
        /// <summary>
        /// offset = <see cref="LocalOffset"/> + 20
        /// </summary>
#endif
        public uint una
        {
            get
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    return *(uint*)(LocalOffset + 16 + ptr);
#else
                    return *(uint*)(LocalOffset + 20 + ptr);
#endif
                }
            }
            set
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    *(uint*)(LocalOffset + 16 + ptr) = value;
#else
                    *(uint*)(LocalOffset + 20 + ptr) = value;
#endif
                }
            }
        }

        // miHoMo KCP modify: IUINT32 token
        // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
        /// <summary>
        /// <para> AppendDateSize </para>
        /// offset = <see cref="LocalOffset"/> + 20
        /// </summary>
#else
        /// <summary>
        /// offset = <see cref="LocalOffset"/> + 24
        /// </summary>
#endif
        public uint len
        {
            get
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    return *(uint*)(LocalOffset + 20 + ptr);
#else
                    return *(uint*)(LocalOffset + 24 + ptr);
#endif
                }
            }
            private set
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    *(uint*)(LocalOffset + 20 + ptr) = value;
#else
                    *(uint*)(LocalOffset + 24 + ptr) = value;
#endif
                }
            }
        }

#if BYTE_CHECK_MODE
        // miHoMo KCP modify: IUINT32 token
        // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
        /// <summary>
        /// <para> AppendDateSize </para>
        /// offset = <see cref="LocalOffset"/> + 24
        /// </summary>
#else
        /// <summary>
        /// offset = <see cref="LocalOffset"/> + 28
        /// </summary>
#endif
        public uint byteCheckCode
        {
            get
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    return *(uint*)(LocalOffset + 24 + ptr);
#else
                    return *(uint*)(LocalOffset + 28 + ptr);
#endif
                }
            }
            private set
            {
                unsafe
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.h, +271
#if !MIHOMO_KCP
                    *(uint*)(LocalOffset + 24 + ptr) = value;
#else
                    *(uint*)(LocalOffset + 28 + ptr) = value;
#endif
                }
            }
        }
#endif

        /// <summary>
        /// 
        /// </summary>
        /// https://github.com/skywind3000/kcp/issues/35#issuecomment-263770736
        public Span<byte> data
        {
            get
            {
                unsafe
                {
                    return new Span<byte>(LocalOffset + HeadOffset + ptr, (int)len);
                }
            }
        }

#if BYTE_CHECK_MODE
        public void ComputeByteCheckCodeFromData()
        {
            switch (byteCheckMode)
            {
                case 1:
                    var buffer = data;
                    byteCheckCode = Crc32.HashToUInt32(buffer);
                    break;
                case 2:
                    var buffer2 = data;
                    byteCheckCode = (uint)XxHash64.HashToUInt64(buffer2);
                    break;
                default:
                    byteCheckCode = 0;
                    break;
            }
        }
#endif

        /// <summary>
        /// 将片段中的要发送的数据拷贝到指定缓冲区
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public int Encode(Span<byte> buffer)
        {
            var datelen = (int)(HeadOffset + len);
#if BYTE_CHECK_MODE
            if (byteCheckCode == 0) ComputeByteCheckCodeFromData();
#endif

            ///备用偏移值 现阶段没有使用
            const int offset = 0;

            if (KcpConst.IsLittleEndian)
            {
                if (BitConverter.IsLittleEndian)
                {
                    ///小端可以一次拷贝
                    unsafe
                    {
                        ///要发送的数据从LocalOffset开始。
                        ///本结构体调整了要发送字段和单机使用字段的位置，让报头数据和数据连续，节约一次拷贝。
                        Span<byte> sendDate = new Span<byte>(ptr + LocalOffset, datelen);
                        sendDate.CopyTo(buffer);
                    }
                }
                else
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.c, +914
#if !MIHOMO_KCP
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset), conv);
                    buffer[offset + 4] = cmd;
                    buffer[offset + 5] = frg;
                    BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(offset + 6), wnd);

                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 8), ts);
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 12), sn);
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 16), una);
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 20), len);
#if BYTE_CHECK_MODE
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 24), byteCheckCode);
#endif
#else
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset), conv);
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset), token);
                    buffer[offset + 8] = cmd;
                    buffer[offset + 9] = frg;
                    BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(offset + 10), wnd);

                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 12), ts);
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 16), sn);
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 20), una);
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 24), len);
#if BYTE_CHECK_MODE
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset + 28), byteCheckCode);
#endif
#endif
                    data.CopyTo(buffer.Slice(HeadOffset));
                }
            }
            else
            {
                if (BitConverter.IsLittleEndian)
                {
                    // miHoMo KCP modify: IUINT32 token
                    // Change line(s) in file compare: ikcp.c, +914
#if !MIHOMO_KCP
                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset), conv);
                    buffer[offset + 4] = cmd;
                    buffer[offset + 5] = frg;
                    BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(offset + 6), wnd);

                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset + 8), ts);
                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset + 12), sn);
                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset + 16), una);
                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset + 20), len);
#if BYTE_CHECK_MODE
                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset + 24), byteCheckCode);
#endif
#else
                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset), conv);
                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset), token);
                    buffer[offset + 8] = cmd;
                    buffer[offset + 9] = frg;
                    BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(offset + 10), wnd);

                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset + 12), ts);
                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset + 16), sn);
                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset + 20), una);
                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset + 24), len);
#if BYTE_CHECK_MODE
                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset + 28), byteCheckCode);
#endif
#endif
                    data.CopyTo(buffer.Slice(HeadOffset));
                }
                else
                {
                    ///大端可以一次拷贝
                    unsafe
                    {
                        ///要发送的数据从LocalOffset开始。
                        ///本结构体调整了要发送字段和单机使用字段的位置，让报头数据和数据连续，节约一次拷贝。
                        Span<byte> sendDate = new Span<byte>(ptr + LocalOffset, datelen);
                        sendDate.CopyTo(buffer);
                    }
                }
            }

            return datelen;
        }
    }
}
