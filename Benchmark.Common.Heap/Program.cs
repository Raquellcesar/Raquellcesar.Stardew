// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

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
