﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OARestClientLib.NounObject;

namespace OARestClientLib
{
    public class SizeNoun : RestAPI<SizeObject>
    {

        public SizeNoun(string baseURL, string username, string password)
            : base(baseURL, username, password)
        {
            _nounURL += SIZES_ENDPOINT;
        }
    }
}