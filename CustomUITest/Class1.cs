using System;
using System.Collections.Generic;
using Dynamo.Graph.Nodes;
using ProtoCore.AST.AssociativeAST;
using CustomUIFunc;
using Dynamo.Engine;
using Dynamo.Scheduler;
using Dynamo.Visualization;
using Autodesk.DesignScript.Interfaces;
using ProtoCore.Mirror;

namespace LINE.DynamoNodes
{
    /// <summary>
    /// Node to filter a list of Revit Elements by their associated User Workset (typical user created worksets).
    /// </summary>
    [NodeName("Mesh From Element Preview")]
    [NodeDescription("Extract a mesh from a Revit element.")]
    [NodeCategory("Preview Nodes")]
    [IsDesignScriptCompatible]
    public class MeshFromElement : NodeModel
    {
        List<Autodesk.DesignScript.Geometry.Mesh> _meshes;


        public MeshFromElement()
        {

            AddPort(PortType.Input, new PortData("elems", "Revit Elements to extract meshes from"), 0);
            AddPort(PortType.Input, new PortData("curView", "Restrict extracted meshes to current view?", AstFactory.BuildBooleanNode(false)), 1);
            AddPort(PortType.Output, new PortData("meshes", "Revit Element Mesh(es)"), 2);
            //RegisterAllPorts();
        }

        public override IEnumerable<AssociativeNode> BuildOutputAst(List<AssociativeNode> inputAstNodes)
        {
            if (!HasConnectedInput(0))
            {
                return new[] { AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), AstFactory.BuildNullNode()) };
            }

            List<AssociativeNode> nodes = new List<AssociativeNode>();
            if (!HasConnectedInput(1))
            {
                nodes.Add(inputAstNodes[0]);
                nodes.Add(AstFactory.BuildBooleanNode(false));
            }
            else
                nodes = inputAstNodes;

            var functionCall = AstFactory.BuildFunctionCall(new Func<List<Revit.Elements.Element>, bool, object>(MeshFromElementFunc.GetMeshes), nodes);

            return new[] { AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), functionCall) };
        }

        public override bool RequestVisualUpdateAsync(IScheduler scheduler, EngineController engine, IRenderPackageFactory factory, bool forceUpdate = true)
        {
            try
            {
                int index = OutPorts[0].Index;
                string name = GetAstIdentifierForOutputIndex(index).Name;
                RuntimeMirror mirror = engine.GetMirror(name);
                MirrorData data = mirror.GetData();

                List<Autodesk.DesignScript.Geometry.Mesh> meshes = GetMeshes(data);
                _meshes = meshes;
                IRenderPackage render = factory.CreateRenderPackage();
                foreach (Autodesk.DesignScript.Geometry.Mesh m in meshes)
                {
                    if (m != null)
                    {
                        m.Tessellate(render, factory.TessellationParameters);
                    }
                }
                return true;
            }
            catch { }
            return false;
        }

        public List<Autodesk.DesignScript.Geometry.Mesh> GetMeshes(MirrorData mirrorData)
        {
            List<Autodesk.DesignScript.Geometry.Mesh> meshes = new List<Autodesk.DesignScript.Geometry.Mesh>();

            if (mirrorData.IsCollection)
            {
                IEnumerable<MirrorData> dataList = mirrorData.GetElements();
                foreach (MirrorData md in dataList)
                {
                    if (md.IsCollection)
                        meshes.AddRange(GetMeshes(md));
                    else
                    {
                        Autodesk.DesignScript.Geometry.Mesh m = md.Data as Autodesk.DesignScript.Geometry.Mesh;
                        if (m != null)
                            meshes.Add(m);
                    }
                }
            }
            else
            {
                Autodesk.DesignScript.Geometry.Mesh m = mirrorData.Data as Autodesk.DesignScript.Geometry.Mesh;
                if (m != null)
                    meshes.Add(m);
            }

            return meshes;
        }
    }
}
