﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OARestClientLib.NounObject
{
    public class ProjectKeywordObject : OARestNounObject
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public long ProjectKeywordCategoryId { get; set; }

        internal ProjectKeywordObject() { }

        internal ProjectKeywordObject(string name, long projectKeywordCategoryId)
        {
            Name = _name = name;
            ProjectKeywordCategoryId = _projectKeywordCategoryId = projectKeywordCategoryId;
        }

        protected override void getVariablesFromParent()
        {
            Id = _id;
            Name = _name;
            ProjectKeywordCategoryId = _projectKeywordCategoryId;
        }
    }
}
