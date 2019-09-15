﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using AttackSurfaceAnalyzer.Types;

namespace AttackSurfaceAnalyzer.Objects
{
    public class ServiceObject : CollectObject
    {
        public string ServiceName { get; set; }
        public string StartType { get; set; }
        public string DisplayName { get; set; }
        public string CurrentState { get; set; }

        public ServiceObject()
        {
            ResultType = RESULT_TYPE.SERVICE;
        }

        public override string Identity
        {
            get
            {
                return ServiceName;
            }
        }
    }
}