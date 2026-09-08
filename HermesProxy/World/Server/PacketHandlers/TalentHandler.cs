using Framework.Logging;
using HermesProxy.World.Enums;
using HermesProxy.World.Server.Packets;

namespace HermesProxy.World.Server;

public partial class WorldSocket
{
    // Modern V3_4_3 CMSG_PET_LEARN_TALENT (0x3554 / 13652) → legacy CMSG_PET_LEARN_TALENT (0x47A).
    // Legacy payload (CMaNGOS PetHandler.cpp:845-855 HandlePetLearnTalent):
    //   ObjectGuid guid; uint32 talent_id; uint32 requested_rank;
    // Modern client sends a separate opcode from CMSG_LEARN_TALENT (which is player-only).
    // Pet GUID is translated modern→legacy via existing GameSessionData.GetLegacyPetGuid
    // (project_pet_guid_fix infrastructure).
    [PacketHandler(Opcode.CMSG_PET_LEARN_TALENT)]
    void HandleLearnPetTalent(LearnPetTalent talent)
    {
        var legacyGuid = GetSession().GameState.GetLegacyPetGuid(talent.PetGUID);
        if (legacyGuid == null)
        {
            Log.Print(LogType.Warn, $"CMSG_PET_LEARN_TALENT: no legacy pet GUID for {talent.PetGUID} — dropping");
            return;
        }

        WorldPacket packet = new WorldPacket(Opcode.CMSG_PET_LEARN_TALENT);
        packet.WriteGuid(legacyGuid.Value);
        packet.WriteUInt32(talent.TalentID);
        packet.WriteUInt32(talent.Rank);
        SendPacketToServer(packet);
    }

    // Modern V3_4_3 CMSG_LEARN_PREVIEW_TALENTS (0x3553 / 13651) → legacy 0x4C1.
    // The 3.4.3 talent UI previews locally and only commits on Apply; that click
    // used to arrive as unmapped 13651 (logged as MSG_NULL_ACTION) and was dropped,
    // so the talent never saved. Legacy body (wow_messages 3.3.5): uint32 count,
    // then count × { uint32 talentId, uint32 rank }. Rank is widened from the
    // modern uint8 TalentInfo.Rank.
    [PacketHandler(Opcode.CMSG_LEARN_PREVIEW_TALENTS)]
    void HandleLearnPreviewTalents(LearnPreviewTalents preview)
    {
        if (preview.Talents.Count == 0)
            return;

        WorldPacket packet = new WorldPacket(Opcode.CMSG_LEARN_PREVIEW_TALENTS);
        PreviewTalentCodec.WriteLegacyList(packet, preview.Talents);
        SendPacketToServer(packet);
    }

    // Modern V3_4_3 CMSG_LEARN_PREVIEW_TALENTS_PET (0x3555 / 13653) → legacy 0x4C2.
    // Same TalentInfo list as the player apply packet, with a leading pet GUID.
    [PacketHandler(Opcode.CMSG_LEARN_PREVIEW_TALENTS_PET)]
    void HandleLearnPetPreviewTalents(LearnPetPreviewTalents preview)
    {
        if (preview.Talents.Count == 0)
            return;

        var legacyGuid = GetSession().GameState.GetLegacyPetGuid(preview.PetGUID);
        if (legacyGuid == null)
        {
            Log.Print(LogType.Warn, $"CMSG_LEARN_PREVIEW_TALENTS_PET: no legacy pet GUID for {preview.PetGUID} — dropping");
            return;
        }

        WorldPacket packet = new WorldPacket(Opcode.CMSG_LEARN_PREVIEW_TALENTS_PET);
        packet.WriteGuid(legacyGuid.Value);
        PreviewTalentCodec.WriteLegacyList(packet, preview.Talents);
        SendPacketToServer(packet);
    }

    // Modern V3_4_3 CMSG_REMOVE_GLYPH (0x32E0 / 13056) → legacy CMSG_REMOVE_GLYPH (0x48A).
    // Both share the same payload: uint8 GlyphSlot (0-5). The legacy server replies with
    // a fresh SMSG_UPDATE_TALENT_DATA, which TalentHandler.HandleTalentsInfoUpdate processes
    // and re-emits SMSG_ACTIVE_GLYPHS — the slot will appear empty in the UI on next refresh.
    [PacketHandler(Opcode.CMSG_REMOVE_GLYPH)]
    void HandleRemoveGlyph(RemoveGlyph remove)
    {
        Log.Print(LogType.Network, $"CMSG_REMOVE_GLYPH: slot={remove.GlyphSlot} → forwarding to legacy");
        WorldPacket packet = new WorldPacket(Opcode.CMSG_REMOVE_GLYPH);
        packet.WriteUInt8(remove.GlyphSlot);
        SendPacketToServer(packet);
    }
}
