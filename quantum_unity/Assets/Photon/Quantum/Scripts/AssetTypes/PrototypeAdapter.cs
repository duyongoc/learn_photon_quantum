using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quantum.Prototypes;

namespace Quantum {
  public abstract class PrototypeAdapter {
    public abstract Type PrototypedType { get; }
  }

  public abstract class PrototypeAdapter<PrototypeType> : PrototypeAdapter where PrototypeType : IPrototype {
    public sealed override Type PrototypedType => typeof(PrototypeType);
    public abstract PrototypeType Convert(EntityPrototypeConverter converter);
  }
}