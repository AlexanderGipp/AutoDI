using Mono.Cecil;

namespace AutoDI.Mono.Cecil
{
    public class Registration
    {
        public TypeDefinition Key { get; }
        public TypeDefinition TargetType { get; }

        public Lifetime Lifetime { get; set; }

        public Registration(TypeDefinition key, TypeDefinition targetType, Lifetime lifetime)
        {
            Key = key;
            TargetType = targetType;
            Lifetime = lifetime;
        }

        public override string ToString()
        {
            return $"{Key.FullName} => {TargetType.FullName} ({Lifetime})";
        }
    }
}