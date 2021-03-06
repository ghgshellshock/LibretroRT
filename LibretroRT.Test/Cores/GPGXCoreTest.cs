﻿using System.Threading.Tasks;
using Xunit;

namespace LibretroRT.Test.Cores
{
    public class GPGXCoreTest : TestBase
    {
        protected const string GenesisRomName = "GenesisGame.md";
        protected const string MegaCDRomName = "MegaCDGame.cue";

        public GPGXCoreTest() : base(() => GPGXRT.GPGXCore.Instance)
        {
        }

        [Theory]
        [InlineData(GenesisRomName)]
        [InlineData(MegaCDRomName)]
        public override Task LoadingRomWorks(string romName)
        {
            return LoadingRomWorksInternal(romName);
        }

        [Theory]
        [InlineData(GenesisRomName)]
        [InlineData(MegaCDRomName)]
        public override Task ExecutionWorks(string romName)
        {
            return ExecutionWorksInternal(romName);
        }
    }
}
