﻿using System;
using Beatmap.Base;
using Beatmap.Base.Customs;
using SimpleJSON;
using LiteNetLib.Utils;

namespace Beatmap.V3.Customs
{
    public class V3Bookmark : BaseBookmark, V3Object
    {

        public override void Serialize(NetDataWriter writer) => throw new NotImplementedException();
        public override void Deserialize(NetDataReader reader) => throw new NotImplementedException();
        public V3Bookmark()
        {
        }

        public V3Bookmark(BaseBookmark other) : base(other)
        {
        }

        public V3Bookmark(JSONNode node) : base(node)
        {
        }

        public V3Bookmark(float time, string name) : base(time, name)
        {
        }

        public override string CustomKeyTrack { get; } = "track";
        public override string CustomKeyColor { get; } = "color";
        public override string KeyTime { get; } = "b";
        public override string KeyName { get; } = "n";
        public override string KeyColor { get; } = "c";

        public override BaseItem Clone() => new V3Bookmark(ToJson().Clone());
    }
}
