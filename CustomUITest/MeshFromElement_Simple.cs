using System;
using System.Collections.Generic;
using Dynamo.Graph.Nodes;
using ProtoCore.AST.AssociativeAST;
using CustomUIFunc;

namespace CustomUITest
{
    /// <summary>
    /// Node to show a simple cube mesh
    /// </summary>
    [NodeName("Cube Mesh")]
    [NodeDescription("Display a cube mesh")]
    [NodeCategory("PreviewTests")]
    [IsDesignScriptCompatible]
    public class MeshFromElement_Simple : NodeModel
    {
        List<Autodesk.DesignScript.Geometry.Mesh> _meshes;

        public MeshFromElement_Simple()
        {
            AddPort(PortType.Input, new PortData("toggle", "toggle to return a cube mesh"), 0);
            AddPort(PortType.Output, new PortData("meshes", "Cube Mesh"), 1);
        }

        public override IEnumerable<AssociativeNode> BuildOutputAst(List<AssociativeNode> inputAstNodes)
        {
            if (!HasConnectedInput(0))
            {
                return new[] { AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), AstFactory.BuildNullNode()) };
            }

            var functionCall = AstFactory.BuildFunctionCall(new Func<bool, Autodesk.DesignScript.Geometry.Mesh>(MeshFromElementFunc.GetSimpleMesh), inputAstNodes);

            return new[] { AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), functionCall) };
        }
    }
}
