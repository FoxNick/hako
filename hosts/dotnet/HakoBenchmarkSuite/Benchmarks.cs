using System;
using System.Threading.Tasks;
using BenchmarkDotNet;
using BenchmarkDotNet.Attributes;
using HakoJS;
using HakoJS.Backend.Wasmtime;
using HakoJS.Extensions;
using HakoJS.Host;
using HakoJS.VM;

namespace HakoBenchmarkSuite
{
    [MemoryDiagnoser] 
    [SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net10_0, baseline: true)]
    [MinIterationCount(15)]
    [MaxIterationCount(20)]
    public class Benchmarks
    {
        private HakoRuntime _runtime;
        private Realm _realm;
        [GlobalSetup]
        public void GlobalSetup()
        {
           _runtime = Hako.Initialize<WasmtimeEngine>();
           _realm = _runtime.CreateRealm();
        }
        
        // Add baseline for comparison
        [Benchmark(Baseline = true)]
        public async ValueTask<double> SimpleEval()
        {
            return await _realm.EvalAsync<double>("1+1");
        }
    
        [Benchmark]
        public async ValueTask<double> ComplexEval()
        {
            return await _realm.EvalAsync<double>("Math.sqrt(144) + 10");
        }
    
        [Benchmark]
        public async ValueTask<string> StringOperation()
        {
            return await _realm.EvalAsync<string>("'hello'.toUpperCase()");
        }
        
        [GlobalCleanup]
        public void GlobalCleanup()
        {
            Hako.ShutdownAsync().GetAwaiter().GetResult();
        }
    }
}