using System;
using System.Threading.Tasks;
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
    public class TypeScriptBenchmarks
    {
        private HakoRuntime _runtime;
        private Realm _realm;
        
        // TypeScript code samples - wrapped in scopes to prevent variable redefinition
        private const string SimpleTypeAnnotation = @"{
            const x: number = 42;
            const y: string = 'hello';
            x + y.length
        }";
        
        private const string InterfaceDefinition = @"{
            interface User {
                name: string;
                age: number;
                email?: string;
            }
            const user: User = { name: 'John', age: 30 };
            user.name + user.age
        }";
        
        private const string GenericFunction = @"{
            function identity<T>(arg: T): T {
                return arg;
            }
            identity<number>(123)
        }";
        
        private const string ComplexTypes = @"{
            type Status = 'active' | 'inactive' | 'pending';
            interface Config {
                timeout: number;
                retries: number;
                status: Status;
            }
            const config: Config = { timeout: 5000, retries: 3, status: 'active' };
            config.timeout + config.retries
        }";
        
        private const string TypeAssertion = @"{
            const data: unknown = { value: 100 };
            const result = (data as { value: number }).value;
            result * 2
        }";

        private const string MultipleTypeFeatures = @"{
            type ID = string | number;
            
            interface Product {
                id: ID;
                name: string;
                price: number;
            }
            
            function calculateTotal<T extends Product>(items: T[]): number {
                return items.reduce((sum, item) => sum + item.price, 0);
            }
            
            const products: Product[] = [
                { id: 1, name: 'Widget', price: 10.50 },
                { id: '2', name: 'Gadget', price: 25.75 },
                { id: 3, name: 'Doohickey', price: 5.25 }
            ];
            
            calculateTotal(products)
        }";

        private const string OptionalChaining = @"{
            interface Address {
                street?: string;
                city?: string;
            }
            
            interface Person {
                name: string;
                address?: Address;
            }
            
            const person: Person = { name: 'Alice' };
            const city: string | undefined = person.address?.city;
            city ?? 'Unknown'
        }";

        private const string IntersectionTypes = @"{
            type Named = { name: string };
            type Aged = { age: number };
            type Person = Named & Aged;
            
            const person: Person = { name: 'Bob', age: 25 };
            person.name.length + person.age
        }";

        private readonly RealmEvalOptions _tsOptions = new RealmEvalOptions
        {
            StripTypes = true
        };

        [GlobalSetup]
        public void GlobalSetup()
        {
            _runtime = Hako.Initialize<WasmtimeEngine>();
            _realm = _runtime.CreateRealm();
        }
        
        // Type Stripping Only Benchmarks (no execution)
        [Benchmark(Baseline = true)]
        public string StripTypes_SimpleAnnotation()
        {
            return _runtime.StripTypes(SimpleTypeAnnotation);
        }
        
        [Benchmark]
        public string StripTypes_Interface()
        {
            return _runtime.StripTypes(InterfaceDefinition);
        }
        
        [Benchmark]
        public string StripTypes_GenericFunction()
        {
            return _runtime.StripTypes(GenericFunction);
        }
        
        [Benchmark]
        public string StripTypes_ComplexTypes()
        {
            return _runtime.StripTypes(ComplexTypes);
        }

        [Benchmark]
        public string StripTypes_MultipleFeatures()
        {
            return _runtime.StripTypes(MultipleTypeFeatures);
        }
        
        // Type Stripping + Execution Benchmarks
        [Benchmark]
        public async ValueTask<int> Eval_SimpleTypeAnnotation()
        {
            return await _realm.EvalAsync<int>(SimpleTypeAnnotation, _tsOptions);
        }
        
        [Benchmark]
        public async ValueTask<string> Eval_InterfaceDefinition()
        {
            return await _realm.EvalAsync<string>(InterfaceDefinition, _tsOptions);
        }
        
        [Benchmark]
        public async ValueTask<int> Eval_GenericFunction()
        {
            return await _realm.EvalAsync<int>(GenericFunction, _tsOptions);
        }
        
        [Benchmark]
        public async ValueTask<int> Eval_ComplexTypes()
        {
            return await _realm.EvalAsync<int>(ComplexTypes, _tsOptions);
        }
        
        [Benchmark]
        public async ValueTask<int> Eval_TypeAssertion()
        {
            return await _realm.EvalAsync<int>(TypeAssertion, _tsOptions);
        }

        [Benchmark]
        public async ValueTask<double> Eval_MultipleTypeFeatures()
        {
            return await _realm.EvalAsync<double>(MultipleTypeFeatures, _tsOptions);
        }

        [Benchmark]
        public async ValueTask<string> Eval_OptionalChaining()
        {
            return await _realm.EvalAsync<string>(OptionalChaining, _tsOptions);
        }

        [Benchmark]
        public async ValueTask<int> Eval_IntersectionTypes()
        {
            return await _realm.EvalAsync<int>(IntersectionTypes, _tsOptions);
        }
        
        [GlobalCleanup]
        public void GlobalCleanup()
        {
            Hako.ShutdownAsync().GetAwaiter().GetResult();
        }
    }
}