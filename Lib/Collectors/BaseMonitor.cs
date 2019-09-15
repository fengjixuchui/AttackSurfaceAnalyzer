﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using AttackSurfaceAnalyzer.Types;


namespace AttackSurfaceAnalyzer.Collectors
{
    public abstract class BaseMonitor : PlatformRunnable
    {
        protected string runId = null;

        protected RUN_STATUS _running = RUN_STATUS.NOT_STARTED;

        public abstract void Start();

        public abstract void Stop();

        public abstract bool CanRunOnPlatform();

        public RUN_STATUS RunStatus()
        {
            return _running;
        }
    }
}