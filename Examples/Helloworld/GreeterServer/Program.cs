// Copyright 2015 gRPC authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Helloworld;
using Google.Protobuf;
using Patch;

namespace GreeterServer
{
    class GreeterPlayer : Greeter.GreeterBase
    {
        static Type GreeterPlayerType = typeof(GreeterPlayer);
        static System.Reflection.MethodInfo GetMethod(string s)
        {
            if (s[0] != '/') throw new RpcException(new Status(StatusCode.Unimplemented, "bad method"));
            int i = s.LastIndexOf('/');
            if (i < 0 || i > s.Length - 2) new RpcException(new Status(StatusCode.Unimplemented, "bad method"));
            // TODO: method can be cached into a hashset to avoid reflecting every call
            System.Reflection.MethodInfo m = GreeterPlayerType.GetMethod(s.Substring(i + 1));
            if (m == null) throw new RpcException(new Status(StatusCode.Unimplemented, "unknown method: " + s.Substring(i + 1)));
            return m;
        }

        public PlayerArchive archive;

        public Task<TResponse> Invoke<TRequest, TResponse>(TRequest request, ServerCallContext context)
        {
            return (Task<TResponse>)GetMethod(context.Method).Invoke(this, new object[]{ request, context });
        }
        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            archive.Counter++;
            archive.History.Add(request.Name);
            return Task.FromResult(new HelloReply{ Message = "Hello " + request.Name });
        }
    }
    
    class GreeterInterceptor : Interceptor
    {
        private readonly PlayerArchive fake_archive = new PlayerArchive { Id = "1024", Name = "Example" };

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            Console.WriteLine("Intercepted: {0}", context.Method);
            try
            {
                //return continuation(request, context); // bypass original service

                byte[] b1;
                byte[] b2;
                byte[] b3;
                byte[] b4;

                using (var stream = new System.IO.MemoryStream())
                {
                    fake_archive.WriteTo(stream);
                    b1 = stream.ToArray();
                }

                GreeterPlayer player = new GreeterPlayer { archive = fake_archive };

                // TODO: load archive from database

                // forward rpc to player instance
                //TResponse response = await player.Invoke<TRequest, TResponse>(request, context);
                TResponse response = await player.Invoke<TRequest,TResponse>(request, context);

                Console.WriteLine("counter: {0}", fake_archive.Counter);
                // TODO: save archive to database

                using (var stream = new System.IO.MemoryStream())
                {
                    fake_archive.WriteTo(stream);
                    b2 = stream.ToArray();
                }

                b3 = Patch.Patch.Diff(b1, b2);
                Console.WriteLine("source={0}, target={1}, patch={2}", b1.Length, b2.Length, b3.Length);

                b4 = Patch.Patch.Merge(b1, b3);
                Console.WriteLine("source={0}, patch={1}, target={2}", b1.Length, b3.Length, b4.Length);

                Console.WriteLine(b2.SequenceEqual(b4) ? "patch correct" : "patch wrong");
                
                return response;
            }
            catch
            {
                Console.WriteLine("Intercepted catch");
                throw;
            }
            finally
            {
                Console.WriteLine("Intercepted finally");
            }
        }
    }

    class GreeterImpl : Greeter.GreeterBase { }

    class Program
    {
        const int Port = 30051;

        public static void Main(string[] args)
        {
            Server server = new Server
            {
                Services =
                {
                    //Greeter.BindService(new GreeterImpl())
                    ServerServiceDefinitionExtensions.Intercept(Greeter.BindService(new GreeterImpl()), new GreeterInterceptor())
                },
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            server.Start();

            Console.WriteLine("Greeter server listening on port " + Port);
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            server.ShutdownAsync().Wait();
        }
    }
}
