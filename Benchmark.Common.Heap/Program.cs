// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.Benchmark.Common

{
    using BenchmarkDotNet.Running;

    using Raquellcesar.Stardew.Benchmark.Common.HeapBenchmark;

    internal class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<Benchmark>();
        }
    }
}
