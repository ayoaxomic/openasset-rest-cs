﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OARestClientLib.NounObject
{
    public class ProjectKeywordValueObject : OARestNounObject
    {
        public long ProjectKeywordId { get; set; }

        protected override void getVariablesFromParent()
        {
            ProjectKeywordId = _projectKeywordId;
        }
    }
}