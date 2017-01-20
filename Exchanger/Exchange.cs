using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace TestConsole
{
    internal class Exchange : IEquatable<Exchange>
    {
        internal string Nickname;
        internal string OfferedOrbName;
        internal string RequestedOrbName;
        internal int OfferedOrbQuantity;
        internal int RequestedOrbQuantity;
        
        public bool Equals(Exchange other)
        {
            if (other == null || this.GetType() != other.GetType())
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Nickname.Equals(other.Nickname) && this.OfferedOrbName.Equals(other.OfferedOrbName) &&
                   this.OfferedOrbQuantity.Equals(other.OfferedOrbQuantity) && this.RequestedOrbName.Equals(other.RequestedOrbName) &&
                   this.RequestedOrbQuantity.Equals(other.RequestedOrbQuantity);

        }
        
        public override int GetHashCode()
        {
            return (this.Nickname?.GetHashCode() ?? 0) ^ (this.OfferedOrbName?.GetHashCode() ?? 0) ^ (this.OfferedOrbQuantity) ^ (this.RequestedOrbName?.GetHashCode() ?? 0) ^ (this.RequestedOrbQuantity);
        }
        
    }
}
