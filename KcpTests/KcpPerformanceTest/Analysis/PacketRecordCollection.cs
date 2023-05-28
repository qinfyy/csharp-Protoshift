﻿using csharp_Protoshift.MhyKCP.Test.Protocol;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace csharp_Protoshift.MhyKCP.Test.Analysis
{
    internal class PacketRecordCollection
    {
        // 由 ack 作为索引的数组
        public ReadOnlyDictionary<uint, ReadOnlyBasePacketRecord> records;
        public ReadOnlyCollection<ReadOnlyBasePacketRecord> records_invalid;
        // ack 格式，client 使用奇数 ack，server 使用偶数 ack
        public readonly bool isClientAck;

        public PacketRecordCollection(IEnumerable<ReadOnlyBasePacketRecord> packetRecords)
        {
            Dictionary<uint, ReadOnlyBasePacketRecord> records_clientAck = new();
            Dictionary<uint, ReadOnlyBasePacketRecord> records_serverAck = new();
            List<ReadOnlyBasePacketRecord> _records_invalid = new();
            foreach (var packetRecord in packetRecords)
            {
                if (!packetRecord.isStructureValid)
                {
                    _records_invalid.Add(packetRecord);
                }
                else
                {
                    bool _isClientAck = packetRecord.ack % 2 == 1;
                    var targetDict = _isClientAck ? records_clientAck : records_serverAck;
                    if (targetDict.ContainsKey(packetRecord.ack))
                    {
                        // 如果当前 record 比字典中更有则取出替换
                        if ((packetRecord.isStructureValid && !targetDict[packetRecord.ack].isStructureValid)
                            || (packetRecord.isStructureValid && targetDict[packetRecord.ack].isStructureValid && packetRecord.isBodyValid && !targetDict[packetRecord.ack].isBodyValid))
                        {
                            _records_invalid.Add(targetDict[packetRecord.ack]);
                            targetDict[packetRecord.ack] = packetRecord;
                        }
                        else
                        {
                            _records_invalid.Add(packetRecord);
                        }
                    }
                    else
                    {
                        targetDict.Add(packetRecord.ack, packetRecord);
                    }
                }
            }
            isClientAck = records_clientAck.Count > records_serverAck.Count;
            if (isClientAck)
            {
                records = new(records_clientAck);
                _records_invalid.AddRange(records_serverAck.Values);
                records_invalid = new(_records_invalid);
            }
            else
            {
                records = new(records_serverAck);
                _records_invalid.AddRange(records_clientAck.Values);
                records_invalid = new(_records_invalid);
            }
        }

        #region Outer Algorithm
        // records_invalid 不参与这里的任何运算

        public static PacketLossResult PacketLoss(PacketRecordCollection send, PacketRecordCollection recv)
        {
            Debug.Assert(!ReferenceEquals(send, recv));
            int sendPacketCount = 0, recvPacketCount = 0;
            List<uint> lost_ack = new(), extra_ack = new();
            #region 数据分析
            foreach (var send_ack in send.records.Keys)
            {
                sendPacketCount++;
                if (recv.records.ContainsKey(recv.FindAck(send_ack, send.isClientAck)))
                {
                    recvPacketCount++;
                }
                else
                {
                    lost_ack.Add(send_ack);
                }
            }
            foreach (var recv_ack in recv.records.Keys)
            {
                if (!send.records.ContainsKey(send.FindAck(recv_ack, recv.isClientAck)))
                {
                    extra_ack.Add(recv_ack);
                }
            }
            #endregion
            if (sendPacketCount == 0) sendPacketCount = -1;
            return new PacketLossResult(send, recv, (double)recvPacketCount / sendPacketCount,
                lost_ack.ToArray(), extra_ack.ToArray(), send.isClientAck, recv.isClientAck);
        }

        public static PacketDelayResult PacketDelay(PacketRecordCollection send, PacketRecordCollection recv)
        {
            Debug.Assert(!ReferenceEquals(send, recv));
            List<(uint, TimeSpan)> ack_list = new();
            int packetCount = 0;
            TimeSpan totalSpan = TimeSpan.Zero;
            #region 数据分析
            foreach (var send_record in send.records.Values)
            {
                var recv_ack = recv.FindAck(send_record.ack, send.isClientAck);
                if (!recv.records.ContainsKey(recv_ack))
                {
                    continue;
                }
                var recv_record = recv.records[recv_ack];
                var interval = recv_record.create_time - send_record.create_time;
                ack_list.Add((send_record.ack, interval));

                packetCount++;
                totalSpan += interval;
            }
            #endregion
            ack_list.Sort();
            return new PacketDelayResult(send, recv, totalSpan / packetCount, ack_list.ToArray());
        }
        #endregion

        /// <summary>
        /// 查找该 ack 在本实例中应对应的 ack，用于算法实现。
        /// 注意这只是纯算法实现而不保证对应 ack 在本实例中存在。
        /// </summary>
        /// <param name="fromAck">发包端提供的ack，一般从另一个实例中获得</param>
        /// <param name="_isClientAck">发包端是否为奇数ack，与<paramref name="fromAck"/>对应</param>
        /// <returns></returns>
        private uint FindAck(uint fromAck, bool _isClientAck)
        {
            Debug.Assert((fromAck % 2 == 1) == _isClientAck);
            fromAck -= _isClientAck ? 0 : 1U;
            return fromAck + (isClientAck ? 0 : 1U);
        }
    }
}