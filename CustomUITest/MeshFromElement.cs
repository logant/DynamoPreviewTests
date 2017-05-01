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
    [NodeCategory("PreviewTests")]
    [IsDesignScriptCompatible]
    public class MeshFromElement : NodeModel, IGraphicItem
    {
        List<Autodesk.DesignScript.Geometry.Mesh> _meshes;
        private Autodesk.DesignScript.Geometry.CoordinateSystem transform { get; set; }

        public MeshFromElement()
        {

            AddPort(PortType.Input, new PortData("elems", "Revit Elements to extract meshes from"), 0);
            AddPort(PortType.Input, new PortData("curView", "Restrict extracted meshes to current view?", AstFactory.BuildBooleanNode(false)), 1);
            AddPort(PortType.Output, new PortData("meshes", "Revit Element Mesh(es)"), 2);

            // This transform is being used for IGraphicItem transform. I'm not sure exactly why a transform that does not change
            // the geometry is necessary, but it's left in place for now.
            transform = Autodesk.DesignScript.Geometry.CoordinateSystem.ByOrigin(0, 0, 0);

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
                        //var method = render.GetType().GetMethod("SetTransform", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new[] { typeof(double[]) }, null);

                        //if (method != null)
                        //{
                        //    method.Invoke(render, new object[] { new double[]
                        //    {
                        //       transform.XAxis.X, transform.XAxis.Y, transform.XAxis.Z, 0,
                        //       transform.YAxis.X, transform.YAxis.Y, transform.YAxis.Z, 0,
                        //       transform.ZAxis.X, transform.ZAxis.Y, transform.ZAxis.Z, 0,
                        //       transform.Origin.X, transform.Origin.Y, transform.Origin.Z, 1
                        //    }
                        //    });
                        //}
                    }
                }
                  
                // NOTE: I'm not sure calling the Tessellate method from IGraphicItem is necessary here
                // but I've tried calling and am leaving it in here just in case I do wind up needing it.
                //Tessellate(render, factory.TessellationParameters);
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

        public void Tessellate(IRenderPackage package, TessellationParameters parameters)
        {

            foreach(Autodesk.DesignScript.Geometry.Mesh m in _meshes)
            {
                m.Tessellate(package, parameters);
            }

            //look for the method SetTransform with the double[] argument list.
            var method = package.GetType().
            GetMethod("SetTransform", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null,
                    new[] { typeof(double[]) }, null);

            //if the method exists call it using our current transform.
            if (method != null)
            {
                method.Invoke(package, new object[] { new double[]
                {transform.XAxis.X,transform.XAxis.Y,transform.XAxis.Z,0,
                transform.YAxis.X,transform.YAxis.Y,transform.YAxis.Z,0,
                transform.ZAxis.X,transform.ZAxis.Y,transform.ZAxis.Z,0,
                transform.Origin.X,transform.Origin.Y,transform.Origin.Z,1
                }});
            }
        }
    }
}
