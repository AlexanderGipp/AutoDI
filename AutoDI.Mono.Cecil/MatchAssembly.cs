﻿using Mono.Cecil;

namespace AutoDI.Mono.Cecil
{
    internal class MatchAssembly
    {
        private readonly Matcher<AssemblyDefinition> _assemblyNameMatcher;

        public MatchAssembly(string assemblyName)
        {
            _assemblyNameMatcher = new Matcher<AssemblyDefinition>(ad => ad.FullName, assemblyName);
        }

        public bool Matches(AssemblyDefinition assemblyName) => _assemblyNameMatcher.TryMatch(assemblyName, out _);

        public override string ToString()
        {
            return _assemblyNameMatcher.ToString();
        }
    }
}