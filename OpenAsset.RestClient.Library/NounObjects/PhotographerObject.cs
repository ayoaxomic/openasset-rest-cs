﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OARestClientLib.NounObject
{
    public class PhotographerObject : OARestNounObject
    {
        public long Id { get; set; }
        public string Name { get; set; }

        internal PhotographerObject() { }

        internal PhotographerObject(string name)
        {
            Name = _name = name;
        }

        protected override void getVariablesFromParent()
        {
            Id = _id;
            Name = _name;
        }
    }
}