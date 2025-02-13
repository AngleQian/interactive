﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Interactive.Connection
{
    public class ConnectNamedPipe : ConnectKernelCommand<NamedPipeConnectionOptions>
    {
        public ConnectNamedPipe() : base("named-pipe",
                                         "Connects to a kernel using named pipes")
        {
            AddOption(new Option<string>("--pipe-name", "The name of the named pipe"));
        }

        public override async Task<Kernel> CreateKernelAsync(NamedPipeConnectionOptions options, KernelInvocationContext context)
        {
            var clientStream = new NamedPipeClientStream(
                ".",
                options.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);

            await clientStream.ConnectAsync();
            clientStream.ReadMode = PipeTransmissionMode.Message;


            var proxyKernel = CreateProxyKernel(options, clientStream);

            return proxyKernel;
        }

        private static ProxyKernel CreateProxyKernel(NamedPipeConnectionOptions options,
            NamedPipeClientStream clientStream)
        {
            var receiver = new KernelCommandAndEventPipeStreamReceiver(clientStream);

            var sender = new KernelCommandAndEventPipeStreamSender(clientStream);
            var proxyKernel = new ProxyKernel(options.KernelName, receiver, sender);

            var _ = proxyKernel.RunAsync();
            return proxyKernel;
        }
    }
}