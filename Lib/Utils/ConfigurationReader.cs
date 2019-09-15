﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace AttackSurfaceAnalyzer.Utils
{
    public class ConfigurationReader
    {
        public static JObject LoadConfigurationFromFile(string filename = "Configuration.json")
        {
            JObject config = null;

            using (StreamReader file = File.OpenText(filename))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                config = (JObject)JToken.ReadFrom(reader);
            }
            if (config == null)
            {
                throw new InvalidDataException("Unable to read configuration file.");
            }

            return config;
        }
    }
}