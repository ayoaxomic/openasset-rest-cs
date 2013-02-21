﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OARestClientLib.NounObject
{
    public class KeywordCategoryObject : OARestNounObject
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public int DisplayOrder { get; set; }
        public long CategoryId { get; set; }

        internal KeywordCategoryObject() { }

        internal KeywordCategoryObject(string name, long categoryId)
        {
            Name = _name = name;
            CategoryId = _categoryId = categoryId;
        }

        protected override void getVariablesFromParent()
        {
            Id = _id;
            Name = _name;
            CategoryId = _categoryId;
            Code = _code;
            DisplayOrder = _displayOrder;
        }
    }
}