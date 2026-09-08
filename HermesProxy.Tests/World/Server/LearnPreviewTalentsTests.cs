using System.Collections.Generic;
using HermesProxy.World;
using HermesProxy.World.Enums;
using HermesProxy.World.Objects;
using HermesProxy.World.Server.Packets;
using Xunit;
using V343 = HermesProxy.World.Enums.V3_4_3_54261;

namespace HermesProxy.Tests.World.Server;

/// <summary>
/// The 3.4.3 talent panel previews locally and only commits when Apply is clicked.
/// That click is CMSG_LEARN_PREVIEW_TALENTS (0x3553 / 13651). It sat unmapped, so
/// the proxy logged MSG_NULL_ACTION and dropped the packet — the talent never saved.
///
/// Wire reference: lineagedr/3.4.3_Source TalentPackets.h/.cpp (uint32 count, then
/// count × { uint32 TalentID, uint8 Rank }). Legacy 3.3.5a widens Rank to uint32.
/// </summary>
public class LearnPreviewTalentsTests
{
    [Fact]
    public void V343_LearnPreviewTalents_SitsBetweenLearnTalentAndPetLearnTalent()
    {
        Assert.Equal(13650u, (uint)V343.Opcode.CMSG_LEARN_TALENT);              // 0x3552
        Assert.Equal(13651u, (uint)V343.Opcode.CMSG_LEARN_PREVIEW_TALENTS);     // 0x3553
        Assert.Equal(13652u, (uint)V343.Opcode.CMSG_PET_LEARN_TALENT);          // 0x3554
        Assert.Equal(13653u, (uint)V343.Opcode.CMSG_LEARN_PREVIEW_TALENTS_PET); // 0x3555
        Assert.NotEqual(0u, (uint)V343.Opcode.CMSG_LEARN_PREVIEW_TALENTS);
        Assert.NotEqual(0u, (uint)V343.Opcode.CMSG_LEARN_PREVIEW_TALENTS_PET);
    }

    [Fact]
    public void LearnPreviewTalents_Read_OneTalent_TalentInfoIsUint32PlusUint8()
    {
        // count=1, TalentID=821 (Arcane Concentration), Rank=0
        var payload = new WorldPacket(1u);
        payload.WriteUInt32(1);
        payload.WriteUInt32(821);
        payload.WriteUInt8(0);

        using var packet = new LearnPreviewTalents(new WorldPacket(Frame(payload.GetData()!)));
        packet.Read();

        Assert.Single(packet.Talents);
        Assert.Equal(821u, packet.Talents[0].TalentID);
        Assert.Equal(0u, packet.Talents[0].Rank);
    }

    [Fact]
    public void LearnPreviewTalents_Read_TwoTalents_NoPaddingBetweenEntries()
    {
        var payload = new WorldPacket(1u);
        payload.WriteUInt32(2);
        payload.WriteUInt32(821);
        payload.WriteUInt8(0);
        payload.WriteUInt32(74);
        payload.WriteUInt8(2);

        using var packet = new LearnPreviewTalents(new WorldPacket(Frame(payload.GetData()!)));
        packet.Read();

        Assert.Equal(2, packet.Talents.Count);
        Assert.Equal(821u, packet.Talents[0].TalentID);
        Assert.Equal(0u, packet.Talents[0].Rank);
        Assert.Equal(74u, packet.Talents[1].TalentID);
        Assert.Equal(2u, packet.Talents[1].Rank);
    }

    [Fact]
    public void LearnPreviewTalents_Read_CapsAtSixty()
    {
        var payload = new WorldPacket(1u);
        payload.WriteUInt32(61);
        for (uint i = 0; i < 61; i++)
        {
            payload.WriteUInt32(1000 + i);
            payload.WriteUInt8(0);
        }

        using var packet = new LearnPreviewTalents(new WorldPacket(Frame(payload.GetData()!)));
        packet.Read();

        Assert.Equal(PreviewTalentCodec.MaxTalents, packet.Talents.Count);
        Assert.Equal(1000u, packet.Talents[0].TalentID);
        Assert.Equal(1059u, packet.Talents[^1].TalentID);
    }

    [Fact]
    public void WriteLegacyList_WidensRankToUint32()
    {
        var talents = new List<TalentEntry>
        {
            new() { TalentID = 821, Rank = 0 },
            new() { TalentID = 74, Rank = 2 },
        };

        var packet = new WorldPacket(1u);
        PreviewTalentCodec.WriteLegacyList(packet, talents);

        using var reader = new WorldPacket(Frame(packet.GetData()!));
        Assert.Equal(2u, reader.ReadUInt32());
        Assert.Equal(821u, reader.ReadUInt32());
        Assert.Equal(0u, reader.ReadUInt32());
        Assert.Equal(74u, reader.ReadUInt32());
        Assert.Equal(2u, reader.ReadUInt32());
        Assert.False(reader.CanRead());
    }

    [Fact]
    public void LearnPetPreviewTalents_Read_PackedGuidThenTalentList()
    {
        var pet = WowGuid128.Create(HighGuidType703.Pet, 0, 1, 42);
        var payload = new WorldPacket(1u);
        payload.WritePackedGuid128(pet);
        payload.WriteUInt32(1);
        payload.WriteUInt32(211);
        payload.WriteUInt8(1);

        using var packet = new LearnPetPreviewTalents(new WorldPacket(Frame(payload.GetData()!)));
        packet.Read();

        Assert.Equal(pet, packet.PetGUID);
        Assert.Single(packet.Talents);
        Assert.Equal(211u, packet.Talents[0].TalentID);
        Assert.Equal(1u, packet.Talents[0].Rank);
    }

    static byte[] Frame(byte[] body)
    {
        var framed = new byte[body.Length + 2];
        body.CopyTo(framed, 2);
        return framed;
    }
}
