﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
// serialization stuff
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;

namespace OpenAsset.RestClient.Library.Noun
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Result : Base.BaseNoun
    {
        #region private serializable properties
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore), NestedNounProperty]
        protected int? file_id;
        #endregion

        #region Accessors
        public virtual int FileId
        {
            get { return file_id ?? default(int); }
            set { file_id = value; }
        }
        #endregion

        public override string UniqueCode
        {
            get { return FileId.ToString(); }
            set { FileId = Convert.ToInt32(value); }
        }

        public override string UniqueCodeField
        {
            get { return "file_id"; }
        }
    }
}
