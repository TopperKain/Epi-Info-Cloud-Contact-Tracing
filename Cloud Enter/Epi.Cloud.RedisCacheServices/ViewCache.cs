﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Epi.Cloud.CacheServices
{
    public class ViewCache : RedisCache
    {
        public ViewCache() : base("view_")
        {
        }
    }
}