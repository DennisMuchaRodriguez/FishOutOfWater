using System.Collections.Generic;

// Árbol de comportamiento mínimo para las IA (peces y pájaros).
//
//  Selector  -> prueba hijos en orden hasta que uno NO falle (prioridades)
//  Sequence  -> ejecuta hijos en orden mientras tengan éxito (condición + acción)
//  Condition -> pregunta (éxito / fallo)
//  Action    -> hace algo; puede seguir "Running" varios ticks
//
// Se re-evalúa desde la raíz en cada tick, así las ramas de mayor prioridad
// (ej. "huir") interrumpen a las de menor prioridad (ej. "pasear") al instante.
namespace FishGame.AI
{
    public enum BTStatus { Success, Failure, Running }

    public abstract class BTNode
    {
        public string Name;
        public abstract BTStatus Tick();
    }

    public class BTSelector : BTNode
    {
        readonly List<BTNode> children;
        public BTSelector(string name, params BTNode[] nodes) { Name = name; children = new List<BTNode>(nodes); }

        public override BTStatus Tick()
        {
            foreach (BTNode child in children)
            {
                BTStatus s = child.Tick();
                if (s != BTStatus.Failure) return s;
            }
            return BTStatus.Failure;
        }
    }

    public class BTSequence : BTNode
    {
        readonly List<BTNode> children;
        public BTSequence(string name, params BTNode[] nodes) { Name = name; children = new List<BTNode>(nodes); }

        public override BTStatus Tick()
        {
            foreach (BTNode child in children)
            {
                BTStatus s = child.Tick();
                if (s != BTStatus.Success) return s;
            }
            return BTStatus.Success;
        }
    }

    public class BTCondition : BTNode
    {
        readonly System.Func<bool> predicate;
        public BTCondition(string name, System.Func<bool> predicate) { Name = name; this.predicate = predicate; }
        public override BTStatus Tick() { return predicate() ? BTStatus.Success : BTStatus.Failure; }
    }

    public class BTAction : BTNode
    {
        readonly System.Func<BTStatus> action;
        public BTAction(string name, System.Func<BTStatus> action) { Name = name; this.action = action; }
        public override BTStatus Tick()
        {
            BTStatus s = action();
            // La primera acción que no falla es la que el ave está haciendo (para depurar: BehaviorTree.ActiveAction)
            BehaviorTree t = BehaviorTree.Current;
            if (s != BTStatus.Failure && t != null && t.ActiveAction == null) t.MarkActive(Name);
            return s;
        }
    }

    public class BTInverter : BTNode
    {
        readonly BTNode child;
        public BTInverter(BTNode child) { Name = "Not " + child.Name; this.child = child; }
        public override BTStatus Tick()
        {
            BTStatus s = child.Tick();
            if (s == BTStatus.Success) return BTStatus.Failure;
            if (s == BTStatus.Failure) return BTStatus.Success;
            return s;
        }
    }

    // Ejecuta el árbol y recuerda qué acción quedó activa (útil para depurar en el Inspector)
    public class BehaviorTree
    {
        public readonly BTNode Root;
        public string ActiveAction { get; private set; }

        public BehaviorTree(BTNode root) { Root = root; }

        // El árbol que se está recorriendo ahora (las acciones lo usan para anotar cuál quedó activa)
        public static BehaviorTree Current { get; private set; }

        public BTStatus Tick()
        {
            ActiveAction = null;
            BehaviorTree previous = Current;
            Current = this;
            try { return Root.Tick(); }
            finally { Current = previous; }
        }

        public void MarkActive(string action) { ActiveAction = action; }
    }
}
