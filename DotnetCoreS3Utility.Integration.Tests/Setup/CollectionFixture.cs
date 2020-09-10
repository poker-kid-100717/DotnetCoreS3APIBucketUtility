using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace DotnetCoreS3Utility.Integration.Tests.Setup
{
    [CollectionDefinition("api")]
    public class CollectionFixture : ICollectionFixture<TestContext>
    {
    }
}