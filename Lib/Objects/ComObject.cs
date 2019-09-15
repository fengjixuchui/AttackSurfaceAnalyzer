﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using AttackSurfaceAnalyzer.Types;
using System.Collections.Generic;
using System.Linq;

namespace AttackSurfaceAnalyzer.Objects
{
    public class ComObject : CollectObject
    {
        // From Registry
        public RegistryObject Key { get; set; }
        public List<RegistryObject> Subkeys { get; set; }
        // From filesystem
        public FileSystemObject x86_Binary { get; set; }
        public FileSystemObject x64_Binary { get; set; }
        public string x86_BinaryName { get; set; }
        public string x64_BinaryName { get; set; }

        public ComObject()
        {
            ResultType = RESULT_TYPE.COM;
        }

        public override string Identity
        {
            get
            {
                if (!string.IsNullOrEmpty(x86_BinaryName))
                {
                    return string.Format("{0} : {1}",x86_BinaryName,Key.Identity.Split("\\").Last());
                }
                else if(!string.IsNullOrEmpty(x64_BinaryName))
                {
                    return string.Format("{0} : {1}", x64_BinaryName, Key.Identity.Split("\\").Last());
                }
                else
                {
                    return Key.Identity.Split("\\").Last();
                }
            }
        }
    }
}