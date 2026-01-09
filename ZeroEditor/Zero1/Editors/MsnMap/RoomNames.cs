namespace ZeroEditor.Zero1.Editors.MsnMap
{
    public static class RoomNames
    {
        public static readonly string[] Names = new string[42]
        {
            "R000 - Entrance",
            "R001 - Great Hall",
            "R002 - Rope Hallway",
            "R003 - Fireplace",
            "R004 - Hidden Pass",
            "R005 - Mask Room",
            "R006 - Sq. Garden",
            "R007 - Burial Room",
            "R008 - Walkway",
            "R009 - Storehouse",
            "R010 - Doll Room",
            "R011 - Kimono Room",
            "R012 - Library",
            "R013 - Lamp Hallway",
            "R014 - Buddha Room",
            "R015 - Rubble Room",
            "R016 - Cherry Atrium",
            "R017 - Stairway",
            "R018 - Dungeon",
            "R019 - Moon Shrine",
            "R020 - Attic",
            "R021 - Backyard",
            "R022 - Abyss",
            "R023 - Fish-Tank Room",
            "R024 - Observatory",
            "R025 - Forest Path",
            "R026 - Narukami Shrine",
            "R027 - Corridor",
            "R028 - Banned Path",
            "R029 - Demon Mouth",
            "R030 - Hell Bridge",
            "R031 - Moon Well",
            "R032 - Rope Altar",
            "R033 - Baptism Path",
            "R034 - Hell Gate",
            "R035 - Koto Room",
            "R036 - Blinding Room",
            "R037 - UNKNOWN",
            "R038 - Tatami Room",
            "R039 - ?",
            "R040 - Doll Room",
            "R041 - Anteroom"
        };

        public static string Get(byte roomId)
        {
            if (roomId < Names.Length)
            {
                var s = Names[roomId];
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }

            return $"R{roomId:000}";
        }
    }
}
