﻿using Ncqrs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    public class ConfigurationFixture
    {
        public ConfigurationFixture()
        {
			NcqrsEnvironment.Deconfigure();
			Configuration.Configure();
        }
    }
}
