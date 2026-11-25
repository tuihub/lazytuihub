import {createFileRegistry, fromBinary, getOption} from '@bufbuild/protobuf';
import {FileDescriptorSetSchema} from '@bufbuild/protobuf/wkt';

const pascal = name => name.replace(/(?:^|_)([a-z0-9])/g, (_, c) => c.toUpperCase());

export function readDescriptors(bytes) {
    return createFileRegistry(fromBinary(FileDescriptorSetSchema, bytes));
}

function property(message, path) {
    const parts = [];
    for (const segment of path.split('.')) {
        const field = message?.fields.find(f => f.name === segment || f.jsonName === segment);
        if (!field) throw new Error(`Unknown request field ${path} in the HTTP binding.`);
        parts.push(pascal(field.name));
        message = field.message;
    }
    return 'request.' + parts.join('.');
}

// gnostic expands {name=feeds/*} into feeds/{feed}. Pair its path parameters with
// the original request field before calling NSwag's flattened overload.
function pathArguments(registry, method, template, openPath, parsers, namesClass) {
    const argumentsByName = new Map();
    const parameters = [...openPath.matchAll(/\{([^}]+)\}/g)].map(m => m[1]);
    let next = 0;
    const expanded = template.replace(/\{([^}=]+)(?:=([^}]+))?\}/g, (_, field, pattern) => {
        const value = property(method.input, field);
        if (!pattern || pattern === '*') {
            const key = parameters[next++];
            argumentsByName.set(key, value);
            return `{${key}}`;
        }
        const keys = [];
        const segments = pattern.split('/').map(segment => {
            if (segment !== '*') return {literal: segment};
            const key = parameters[next++];
            keys.push(key);
            return {key};
        });
        const format = segments.map(s => s.literal ?? `{${s.key}}`).join('/');
        for (const key of keys) {
            const match = segments.map(s => s.literal !== undefined ? JSON.stringify(s.literal)
                : '{ Length: > 0 }' + (s.key === key ? ' id' : '')).join(', ');
            const ref = method.input.fields.find(f => f.name === field);
            const resource = ref && getOption(ref, registry.getExtension('google.api.resource_reference')).type;
            const type = resource?.split('/').pop() || 'Resource';
            const reason = 'INVALID_' + type.replace(/([a-z0-9])([A-Z])/g, '$1_$2').toUpperCase() + '_NAME';
            const identity = JSON.stringify([format, key, reason]);
            let parser = parsers.get(identity)?.name;
            if (!parser) {
                const base = pascal(key) + 'Id';
                parser = [...parsers.values()].some(p => p.name === base) ? method.name + pascal(field) + base : base;
                const source = `        internal static string ${parser}(string name) =>
            name.Split('/') is [${match}] ? id : throw global::LazyTuiHub.Api.RpcErrors.Create(
                global::Grpc.Core.StatusCode.InvalidArgument, "${reason}", "Expected a resource name of the form ${format}.");`;
                parsers.set(identity, {name: parser, source});
            }
            argumentsByName.set(key, `${namesClass}.${parser}(${value})`);
        }
        return format;
    });
    if (expanded !== openPath || next !== parameters.length)
        throw new Error(`Cannot map HTTP template ${template} to ${openPath} for ${method.name}.`);
    return argumentsByName;
}

export function requestBindings(registry, service, config, paths) {
    const descriptor = [...registry].find(d => d.kind === 'service' && d.name === service
        && config.additionalNamespaceUsages.includes(d.file.proto.options.csharpNamespace));
    if (!descriptor) throw new Error(`Missing Proto service for ${config.requestInterface}.`);
    const apiNamespace = config.requestInterface.slice(0, config.requestInterface.lastIndexOf('.'));
    const namesClass = service + 'Paths';
    const parsers = new Map();
    const methods = [];
    for (const method of descriptor.methods) {
        const rule = getOption(method, registry.getExtension('google.api.http'));
        const route = Object.entries(paths).find(([, operations]) =>
            operations[rule.pattern.case]?.operationId === service + '_' + method.name);
        if (!route) throw new Error(`Missing OpenAPI HTTP binding for ${descriptor.typeName}.${method.name}.`);
        const [path, operations] = route;
        const operation = operations[rule.pattern.case];
        const args = pathArguments(registry, method, rule.pattern.value, path, parsers, namesClass);
        for (const parameter of operation.parameters ?? []) {
            if (parameter.in === 'query') args.set(parameter.name, property(method.input, parameter.name));
        }
        if (rule.body) args.set('body', rule.body === '*' ? 'request' : property(method.input, rule.body));
        // A whole-request body without other arguments already implements the interface.
        if (rule.body === '*' && args.size === 1) continue;
        args.set('cancellationToken', 'cancellationToken');
        const call = [...args].map(([key, value]) => `@${key}: ${value}`).join(', ');
        methods.push(`        public Task<${method.output.name}> ${method.name}Async(${method.input.name} request, CancellationToken cancellationToken = default) =>
            ${method.name}Async(${call});`);
    }
    const protoNamespace = descriptor.file.proto.options.csharpNamespace;
    const forwards = descriptor.methods.map(method =>
        `        public override Task<${method.output.name}> ${method.name}(${method.input.name} request, global::Grpc.Core.ServerCallContext context) =>
            application.${method.name}Async(request, context.CancellationToken);`);
    const helpers = parsers.size ? `
    internal static class ${namesClass}
    {
${[...parsers.values()].map(p => p.source).join('\n\n')}
    }
` : '';
    const shared = `namespace ${apiNamespace}
{
    using System.Threading.Tasks;
    using ${protoNamespace};

    internal sealed class ${service}Endpoint(global::${config.requestInterface} application)
        : global::${protoNamespace}.${service}.${service}Base
    {
${forwards.join('\n\n')}
    }
${helpers}}
`;
    const client = `
namespace ${config.namespace}
{
    using System.Threading;
    using System.Threading.Tasks;
    using ${apiNamespace};
${config.additionalNamespaceUsages.map(ns => '    using ' + ns + ';').join('\n')}

    public partial class ${config.className} : global::${config.requestInterface}
    {
        static partial void UpdateJsonSerializerSettings(System.Text.Json.JsonSerializerOptions settings) =>
            settings.Converters.Add(new global::LazyTuiHub.Client.ProtobufJsonConverter());
${methods.length ? '\n' + methods.join('\n\n') + '\n' : ''}    }
}
`;
    return {client, shared};
}
