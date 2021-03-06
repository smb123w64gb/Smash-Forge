using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using OpenTK.Input;
using System.Security.Cryptography;
using SALT.Scripting.AnimCMD;
using OpenTK.Graphics;
using System.Diagnostics;
using WeifenLuo.WinFormsUI.Docking;
using System.IO;

namespace Smash_Forge
{
    public partial class VBNViewport : DockContent
    {
        int defaulttex = 0;

        public VBNViewport()
        {
            InitializeComponent();
            this.TabText = "Renderer";
            Hitboxes = new SortedList<int, Hitbox>();
            Application.Idle += Application_Idle;
            Runtime.AnimationChanged += Runtime_AnimationChanged;
        }

        // Explicitly unsubscribe from the static event to 
        // prevent hanging Garbage Collection.
        ~VBNViewport()
        {
            Runtime.AnimationChanged -= Runtime_AnimationChanged;
            Application.Idle -= Application_Idle;
        }

        public bool _controlLoaded;
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (!DesignMode)
            {
                SetupViewPort();
            }
            render = true;
            _controlLoaded = true;
            defaulttex = NUT.loadImage(SForge.Properties.Resources.DefaultTexture);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!DesignMode && _controlLoaded)
            {
                GL.LoadIdentity();
                GL.Viewport(glControl1.ClientRectangle);


                v = Matrix4.CreateRotationY(0.5f * rot) * Matrix4.CreateRotationX(0.2f * lookup) * Matrix4.CreateTranslation(5 * width, -5f - 5f * height, -15f + zoom) * Matrix4.CreatePerspectiveFieldOfView(1.3f, Width / (float)Height, 1.0f, 500.0f);
            }
        }

        #region Members
        Matrix4 v;
        float rot = 0;
        float x = 0;
        float lookup = 0;
        float height = 0;
        float width = 0;
        float zoom = 0, nzoom = 0;
        float mouseXLast = 0;
        float mouseYLast = 0;
        float mouseSLast = 0;
        bool render = false;
        bool isPlaying = false;
        bool fpsView = false;
        Shader shader;
        #endregion

        #region Event Handlers
        private void Application_Idle(object sender, EventArgs e)
        {
            // Fix accessing the control after 
            // the editor has been closed 
            if (Runtime.killWorkspace)
            {
                foreach (ModelContainer n in Runtime.ModelContainers)
                {
                    n.Destroy();
                }
                foreach (NUT n in Runtime.TextureContainers)
                {
                    n.Destroy();
                }
                Runtime.ModelContainers = new List<ModelContainer>();
                Runtime.TextureContainers = new List<NUT>();
                Runtime.TargetVBN = null;
                Runtime.TargetAnim = null;
                Runtime.TargetLVD = null;
                Runtime.TargetPath = null;
                Runtime.TargetCMR0 = null;
                Runtime.TargetNUD = null;
                Runtime.killWorkspace = false;
                Runtime.Animations = new Dictionary<string, SkelAnimation>();
                MainForm.Instance.lvdList.fillList();
                MainForm.animNode.Nodes.Clear();
                MainForm.Instance.mtaNode.Nodes.Clear();
                MainForm.Instance.meshList.refresh();
                MainForm.Instance.paramEditors = new List<PARAMEditor>();
                if (Directory.Exists("workspace/animcmd/"))
                {
                    foreach (string file in Directory.EnumerateFiles("workspace/animcmd/"))
                        File.Delete(file);
                    Directory.Delete("workspace/animcmd/");
                }

                MainForm.Instance.project.fillTree();
            }

            if (this.IsDisposed == true)
                return;

            while (render && _controlLoaded && glControl1.IsIdle)
            {
                if (isPlaying)
                {
                    if (nupdFrame.Value < nupdMaxFrame.Value)
                    {
                        nupdFrame.Value++;
                    }
                    else if (nupdFrame.Value == nupdMaxFrame.Value)
                    {
                        if (!checkBox2.Checked)
                        {
                            isPlaying = false;
                            btnPlay.Text = "Play";
                        }
                        else
                        {
                            nupdFrame.Value = 0;
                        }
                    }
                }
                Render();
                System.Threading.Thread.Sleep(1000 / AnimationSpeed);
            }
        }
        private void Runtime_AnimationChanged(object sender, EventArgs e)
        {
            loadAnimation(Runtime.TargetAnim);
        }

        private void btnFirstFrame_Click(object sender, EventArgs e)
        {
            this.nupdFrame.Value = 0;
        }
        private void btnPrevFrame_Click(object sender, EventArgs e)
        {
            if (this.nupdFrame.Value - 1 >= 0)
                this.nupdFrame.Value -= 1;
        }
        private void btnLastFrame_Click(object sender, EventArgs e)
        {
            if (Runtime.TargetAnim != null)
                this.nupdFrame.Value = Runtime.TargetAnim.size() - 1;
        }
        private void btnNextFrame_Click(object sender, EventArgs e)
        {
            if (Runtime.TargetAnim != null)
                this.nupdFrame.Value += 1;
        }
        private void btnPlay_Click(object sender, EventArgs e)
        {
            // If we're already at final frame and we hit play again
            // start from the beginning of the anim
            if (nupdMaxFrame.Value == nupdFrame.Value)
                nupdFrame.Value = 0;

            isPlaying = !isPlaying;
            if (isPlaying)
            {
                btnPlay.Text = "Pause";
            }
            else
            {
                btnPlay.Text = "Play";
            }
        }
        private void nupdFrame_ValueChanged(Object sender, EventArgs e)
        {
            if (Runtime.TargetAnim == null)
                return;

            if (this.nupdFrame.Value >= Runtime.TargetAnim.size())
            {
                this.nupdFrame.Value = 0;
                return;
            }
            if (this.nupdFrame.Value < 0)
            {
                this.nupdFrame.Value = Runtime.TargetAnim.size() - 1;
                return;
            }
            Runtime.TargetAnim.setFrame((int)this.nupdFrame.Value);

            foreach (ModelContainer m in Runtime.ModelContainers)
            {
                Runtime.TargetAnim.setFrame((int)this.nupdFrame.Value);
                if (m.vbn != null)
                    Runtime.TargetAnim.nextFrame(m.vbn);

                if (m.dat_melee != null)
                {
                    Runtime.TargetAnim.setFrame((int)this.nupdFrame.Value);
                    Runtime.TargetAnim.nextFrame(m.dat_melee.bones);
                }
                if (m.bch != null)
                {
                    foreach (BCH.BCH_Model mod in m.bch.models)
                    {
                        Runtime.TargetAnim.setFrame((int)this.nupdFrame.Value);
                        if (mod.skeleton != null)
                            Runtime.TargetAnim.nextFrame(mod.skeleton);
                    }
                }
            }

            Frame = (int)this.nupdFrame.Value;

            if (script != null)
                ProcessFrame();
        }
        private void nupdSpeed_ValueChanged(object sender, EventArgs e)
        {
            AnimationSpeed = (int)nupdFrameRate.Value;
        }

        private void glControl1_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!fpsView)
                UpdateMousePosition();
        }

        public event EventHandler FrameChanged;
        protected virtual void OnFrameChanged(EventArgs e)
        {
            if (FrameChanged != null)
                FrameChanged(this, e);
            //HandleACMD(Runtime.TargetAnimString);
        }
        #endregion

        #region Properties
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal int Frame
        {
            get
            {
                return _frame;
            }
            set
            {
                _frame = value;
                OnFrameChanged(new EventArgs());
            }
        }
        private int _frame = 0;

        [DefaultValue(60)]
        internal int AnimationSpeed { get { return _animSpeed; } set { _animSpeed = value; } }
        private int _animSpeed = 60;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        private SortedList<int, Hitbox> Hitboxes { get; set; }
        #endregion

        #region Rendering

        string vs = @"#version 330
 
in vec3 vPosition;
in vec4 vColor;
in vec3 vNormal;
in vec2 vUV;
in vec4 vBone;
in vec4 vWeight;

out vec2 f_texcoord;
out float normal;
out vec4 color;
out float fresNelR;

uniform mat4 modelview;
uniform mat4 bones[200];

uniform int renderType;
 
void
main()
{
    ivec4 index = ivec4(vBone); 

    vec4 objPos = vec4(vPosition.xyz, 1.0);

    if(vBone.x != -1){
        objPos = bones[index.x] * vec4(vPosition, 1.0) * vWeight.x;
        objPos += bones[index.y] * vec4(vPosition, 1.0) * vWeight.y;
        objPos += bones[index.z] * vec4(vPosition, 1.0) * vWeight.z;
        objPos += bones[index.w] * vec4(vPosition, 1.0) * vWeight.w;
    } 

    gl_Position = modelview * vec4(objPos.xyz, 1.0);

    if(renderType == 1){
        f_texcoord = vUV;
        normal = 0;
        color = vec4(vNormal, 1);
    }else{
        vec3 distance = (objPos.xyz + vec3(5, 5, 5))/2;

        f_texcoord = vUV;
        normal = dot(vec4(vNormal * mat3(modelview), 1.0), vec4(0.15,0.15,0.15,1.0)) ;// vec4(distance, 1.0)
        if(renderType == 2)
            color = vec4(normal, normal, normal, 1);
        else
            color = vColor;

        vec4 normWorld = normalize(vec4(vNormal, 1.0));
	    vec4 I = normalize(vec4(vPosition, 1.0));
        fresNelR = 0.2 + 0.2 * pow(1.0 + dot(I, normWorld), 1);
    }
}";

        string fs = @"#version 330

in vec2 f_texcoord;
in vec4 color;
in float normal;
in float fresNelR;

uniform sampler2D tex;
uniform sampler2D nrm;
uniform vec4 colorSamplerUV;
uniform vec4 colorOffset;
uniform vec4 colorGain;
uniform vec4 minGain;

uniform int renderType;

vec4 lerp(float v, vec4 from, vec4 to)
{
    return from + (to - from) * v;
}

void
main()
{
    if(renderType == 1 || renderType == 2 || renderType == 3){
        gl_FragColor = color;
    } else {
        vec2 texcoord = vec2((f_texcoord * colorSamplerUV.xy) + colorSamplerUV.zw) ;

        vec3 norm = 2.0 * texture2D (nrm, texcoord).rgb - 1.0;
        norm = normalize (norm);
        float lamberFactor= max (dot (vec3(0.85, 0.85, 0.85), norm), 0.75) * 1.5;

        //vec4 ambiant = vec4(0.1,0.1,0.1,1.0) * texture(tex, texcoord).rgba;

        vec4 alpha = (1-minGain) + texture2D(nrm, texcoord).aaaa;
        //if(alpha.a < 0.5) discard;

        vec4 fcolor = color;
        float fnormal = normal;

        if(renderType == 0x20 || renderType == 0x60)
            fnormal = 1;
        if(renderType == 0x40 || renderType == 0x60)
            fcolor = vec4(1,1,1,1);

	    vec4 outputColor = colorOffset + (vec4(texture(tex, texcoord).rgba) * fnormal) * colorGain;
        vec4 fincol = vec4(((fcolor * alpha * outputColor)).xyz, texture2D(tex, texcoord).a * fcolor.w);
        gl_FragColor = fincol;//vec4(lerp(fresNelR, fincol, vec4(1.75,1.75,1.75,1)).xyz, fincol.w);
    }
}
";

        private void SetupViewPort()
        {

            if (shader != null)
                GL.DeleteShader(shader.programID);

            if (shader == null)
            {
                shader = new Shader();

                {
                    if (GL.GetInteger(GetPName.MajorVersion) < 3 || (GL.GetInteger(GetPName.MajorVersion) == 3 && GL.GetInteger(GetPName.MinorVersion) < 3))
                    {
                        shader.vertexShader(vs.Replace("#version 330", "#version 130"));
                        shader.fragmentShader(fs.Replace("#version 330", "#version 130"));
                    }
                    else
                    {
                        shader.vertexShader(vs);
                        shader.fragmentShader(fs);
                    }

                    shader.addAttribute("vPosition", false);
                    shader.addAttribute("vColor", false);
                    shader.addAttribute("vNormal", false);
                    shader.addAttribute("vUV", false);
                    shader.addAttribute("vBone", false);
                    shader.addAttribute("vWeight", false);

                    shader.addAttribute("tex", true);
                    shader.addAttribute("nrm", true);
                    shader.addAttribute("modelview", true);
                    shader.addAttribute("bones", true);
                    shader.addAttribute("colorSamplerUV", true);
                    shader.addAttribute("colorOffset", true);
                    shader.addAttribute("colorGain", true);
                    shader.addAttribute("minGain", true);

                    shader.addAttribute("renderType", true);
                }
            }

            int h = Height - groupBox2.ClientRectangle.Top;
            int w = Width;
            GL.LoadIdentity();
            GL.Viewport(glControl1.ClientRectangle);
            v = Matrix4.CreateRotationY(0.5f * rot) * Matrix4.CreateRotationX(0.2f * lookup) * Matrix4.CreateTranslation(5 * width, -5f - 5f * height, -15f + zoom) 
                * Matrix4.CreatePerspectiveFieldOfView(1.3f, Width / (float)Height, 1.0f, Runtime.renderDepth);

        }

        int cf = 0;
        Color back1 = Color.FromArgb((255 << 24) | (26 << 16) | (26 << 8) | (26));
        Color back2 = Color.FromArgb((255 << 24) | (77 << 16) | (77 << 8) | (77));

        public void Render()
        {
            if (!render)
                return;

            glControl1.MakeCurrent();

            //GL.ClearColor(back1);
            // Push all attributes so we don't have to clean up later
            GL.PushAttrib(AttribMask.AllAttribBits);
            // clear the gf buffer
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            GL.Begin(PrimitiveType.Quads);
            GL.Color3(back1);
            GL.Vertex2(1.0, 1.0);
            GL.Vertex2(-1.0, 1.0);
            GL.Color3(back2);
            GL.Vertex2(-1.0, -1.0);
            GL.Vertex2(1.0, -1.0);
            GL.End();

            //GL.DepthFunc(DepthFunction.Never);
            GL.Enable(EnableCap.DepthTest);
            GL.ClearDepth(1.0);

            // set up the viewport projection and send it to GPU
            GL.MatrixMode(MatrixMode.Projection);

            if (IsMouseOverViewport() && glControl1.Focused)
            {
                if (fpsView)
                    FPSCamera();
                else
                    UpdateMousePosition();
            }
            mouseSLast = OpenTK.Input.Mouse.GetState().WheelPrecise;

            SetCameraAnimation();

            GL.LoadMatrix(ref v);
            // ready to start drawing model stuff
            GL.MatrixMode(MatrixMode.Modelview);

            GL.UseProgram(0);
            // drawing floor---------------------------
            if (Runtime.renderFloor)
                RenderTools.drawFloor(Matrix4.CreateTranslation(Vector3.Zero));

            //RenderTools.drawSphere(new Vector3(2,2,2), 3, 5);

            //GL.Enable(EnableCap.LineSmooth); // This is Optional 
            GL.Enable(EnableCap.Normalize);  // These is critical to have
            GL.Enable(EnableCap.RescaleNormal);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);

            GL.Enable(EnableCap.AlphaTest);
            GL.AlphaFunc(AlphaFunction.Gequal, 0.1f);

            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Front);

            // draw models
            if (Runtime.renderModel)
                DrawModels();

            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.DepthFunc(DepthFunction.Less);
            GL.AlphaFunc(AlphaFunction.Gequal, 0.1f);
            GL.Disable(EnableCap.CullFace);

            GL.UseProgram(0);
            // draw path.bin
            if (Runtime.renderPath)
                DrawPathDisplay();
            // clear the buffer bit so the skeleton 
            // will be drawn on top of everything
            GL.Clear(ClearBufferMask.DepthBufferBit);
            // draw lvd
            if (Runtime.renderLVD)
                DrawLVD();
            // drawing the bones
            DrawBones();

            // Clean up
            GL.PopAttrib();
            glControl1.SwapBuffers();
        }

        public void UpdateMousePosition()
        {
            float zoomscale = 1;

            if ((OpenTK.Input.Mouse.GetState().RightButton == OpenTK.Input.ButtonState.Pressed))
            {
                height += 0.025f * (OpenTK.Input.Mouse.GetState().Y - mouseYLast);
                width += 0.025f * (OpenTK.Input.Mouse.GetState().X - mouseXLast);
            }
            if ((OpenTK.Input.Mouse.GetState().LeftButton == OpenTK.Input.ButtonState.Pressed))
            {
                rot += 0.025f * (OpenTK.Input.Mouse.GetState().X - mouseXLast);
                lookup += 0.025f * (OpenTK.Input.Mouse.GetState().Y - mouseYLast);
            }

            if (OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.ShiftLeft))
                zoomscale = 6;

            if (OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.Down))
                zoom -= 1 * zoomscale;
            if (OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.Up))
                zoom += 1 * zoomscale;

            mouseXLast = OpenTK.Input.Mouse.GetState().X;
            mouseYLast = OpenTK.Input.Mouse.GetState().Y;

            zoom += (OpenTK.Input.Mouse.GetState().WheelPrecise - mouseSLast) * zoomscale;

            v = Matrix4.CreateRotationY(0.5f * rot) * Matrix4.CreateRotationX(0.2f * lookup) * Matrix4.CreateTranslation(5 * width, -5f - 5f * height, -15f + zoom) 
                * Matrix4.CreatePerspectiveFieldOfView(1.3f, Width / (float)Height, 1.0f, Runtime.renderDepth);
        }
        public bool IsMouseOverViewport()
        {
            if (glControl1.ClientRectangle.Contains(PointToClient(Cursor.Position)))
                return true;
            else
                return false;
        }

        private void SetCameraAnimation()
        {
            if (Runtime.TargetPath != null && checkBox1.Checked)
            {
                if (cf >= Runtime.TargetPath.Frames.Count)
                    cf = 0;
                pathFrame f = Runtime.TargetPath.Frames[cf];
                v = (Matrix4.CreateTranslation(f.x, f.y, f.z) * Matrix4.CreateFromQuaternion(new Quaternion(f.qx, f.qy, f.qz, f.qw))).Inverted() 
                    * Matrix4.CreatePerspectiveFieldOfView(1.3f, Width / (float)Height, 1.0f, 90000.0f);
                cf++;
            }
            else if (Runtime.TargetCMR0 != null && checkBox1.Checked)
            {
                if (cf >= Runtime.TargetCMR0.frames.Count)
                    cf = 0;
                Matrix4 m = Runtime.TargetCMR0.frames[cf].Inverted();
                v = Matrix4.CreateTranslation(m.M14, m.M24, m.M34) * Matrix4.CreateFromQuaternion(m.ExtractRotation()) 
                    * Matrix4.CreatePerspectiveFieldOfView(1.3f, Width / (float)Height, 1.0f, 90000.0f);
                cf++;
            }
        }

        private void DrawModels()
        {
            // Bounding Box Render
            /*foreach (ModelContainer m in Runtime.ModelContainers)
            {
                if (m.nud != null)
                {
                    RenderTools.drawCubeWireframe(new Vector3(m.nud.param[0], m.nud.param[1], m.nud.param[2]), m.nud.param[3]);
                    foreach (NUD.Mesh mesh in m.nud.mesh)
                    {
                        RenderTools.drawCubeWireframe(new Vector3(mesh.bbox[0], mesh.bbox[1], mesh.bbox[2]), mesh.bbox[3]);
                    }
                }
            }*/

            GL.UseProgram(shader.programID);
            int rt = (int)Runtime.renderType;
            if(rt == 0)
            {
                if (Runtime.renderNormals)
                    rt = rt | (0x10);
                if (Runtime.renderVertColor)
                    rt = rt | (0x20);
            }
            GL.Uniform1(shader.getAttribute("renderType"), rt);
            foreach (ModelContainer m in Runtime.ModelContainers)
            {
                if (m.bch != null)
                {
                    if (m.bch.mbn != null)
                    {
                        GL.UniformMatrix4(shader.getAttribute("modelview"), false, ref v);

                        // Bone
                        if (m.bch.models.Count > 0)
                        {
                            Matrix4[] f = m.bch.models[0].skeleton.getShaderMatrix();
                            int shad = shader.getAttribute("bone");
                            GL.UniformMatrix4(shad, f.Length, false, ref f[0].Row0.X);
                        }

                        shader.enableAttrib();
                        m.bch.mbn.Render(shader);
                        shader.disableAttrib();
                    }
                }

                if (m.dat_melee != null)
                {
                    m.dat_melee.Render(v);
                }

                if (m.nud != null)
                {
                    GL.UniformMatrix4(shader.getAttribute("modelview"), false, ref v);

                    if (m.vbn != null)
                    {
                        Matrix4[] f = m.vbn.getShaderMatrix();
                        int shad = shader.getAttribute("bone");
                        GL.UniformMatrix4(shad, f.Length, false, ref f[0].Row0.X);
                    }

                    shader.enableAttrib();
                    m.nud.clearMTA();//Clear animated materials

                    if (m.mta != null)
                        m.nud.applyMTA(m.mta, (int)nupdFrame.Value);//Apply base mta
                    if (Runtime.TargetMTA != null)
                        m.nud.applyMTA(Runtime.TargetMTA, (int)nupdFrame.Value);//Apply additional mta (can override base)

                    m.nud.Render(shader);
                    shader.disableAttrib();
                }
            }
        }

        private void DrawBones()
        {
            if (Runtime.ModelContainers.Count > 0)
            {
                // Render the hitboxes
                if (!string.IsNullOrEmpty(Runtime.TargetAnimString))
                    HandleACMD(Runtime.TargetAnimString.Substring(3));

                if (Runtime.renderHitboxes)
                    RenderHitboxes();

                foreach (ModelContainer m in Runtime.ModelContainers)
                {
                    DrawVBN(m.vbn);
                    if (m.bch != null)
                    {
                        DrawVBN(m.bch.models[0].skeleton);
                    }

                    if (m.dat_melee != null)
                    {
                        DrawVBN(m.dat_melee.bones);
                    }
                }
            }
        }

        public void DrawVBNDiamond(VBN vbn)
        {
            if (vbn != null && Runtime.renderBones)
            {
                foreach (Bone bone in vbn.bones)
                {
                    // first calcuate the point and draw a point
                    GL.Color3(Color.DarkGray);
                    GL.PointSize(1f);

                    Vector3 pos_c = Vector3.Transform(Vector3.Zero, bone.transform);

                    GL.Begin(PrimitiveType.LineLoop);
                    GL.Vertex3(new Vector3(pos_c.X - 0.1f, pos_c.Y, pos_c.Z - 0.1f));
                    GL.Vertex3(new Vector3(pos_c.X + 0.1f, pos_c.Y, pos_c.Z - 0.1f));
                    GL.Vertex3(new Vector3(pos_c.X + 0.1f, pos_c.Y, pos_c.Z + 0.1f));
                    GL.Vertex3(new Vector3(pos_c.X - 0.1f, pos_c.Y, pos_c.Z + 0.1f));
                    GL.End();

                    Vector3 pos_p = pos_c;
                    if (bone.parentIndex != 0x0FFFFFFF && bone.parentIndex != -1)
                    {
                        int i = bone.parentIndex;
                        pos_p = Vector3.Transform(Vector3.Zero, vbn.bones[i].transform);
                    }

                    GL.Color3(Color.Gray);
                    GL.Begin(PrimitiveType.Lines);
                    GL.Vertex3(new Vector3(pos_c.X - 0.1f, pos_c.Y, pos_c.Z - 0.1f));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y + 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X + 0.1f, pos_c.Y, pos_c.Z - 0.1f));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y + 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X + 0.1f, pos_c.Y, pos_c.Z + 0.1f));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y + 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X - 0.1f, pos_c.Y, pos_c.Z + 0.1f));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y + 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X - 0.1f, pos_c.Y, pos_c.Z - 0.1f));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y - 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X + 0.1f, pos_c.Y, pos_c.Z - 0.1f));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y - 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X + 0.1f, pos_c.Y, pos_c.Z + 0.1f));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y - 0.25f, pos_c.Z));
                    GL.Vertex3(new Vector3(pos_c.X - 0.1f, pos_c.Y, pos_c.Z + 0.1f));
                    GL.Vertex3(new Vector3(pos_c.X, pos_c.Y - 0.25f, pos_c.Z));


                    GL.Vertex3(new Vector3(pos_c.X - 0.1f, pos_c.Y, pos_c.Z - 0.1f));
                    GL.Vertex3(pos_p);
                    GL.Vertex3(new Vector3(pos_c.X + 0.1f, pos_c.Y, pos_c.Z - 0.1f));
                    GL.Vertex3(pos_p);
                    GL.Vertex3(new Vector3(pos_c.X + 0.1f, pos_c.Y, pos_c.Z + 0.1f));
                    GL.Vertex3(pos_p);
                    GL.Vertex3(new Vector3(pos_c.X - 0.1f, pos_c.Y, pos_c.Z + 0.1f));
                    GL.Vertex3(pos_p);

                    GL.End();
                }
            }
        }

        public void DrawVBN(VBN vbn)
        {
            if (vbn != null && Runtime.renderBones)
            {
                foreach (Bone bone in vbn.bones)
                {
                    // first calcuate the point and draw a point
                    if(bone == BoneTreePanel.selectedBone)
                        GL.Color3(Color.Red);
                    else
                        GL.Color3(Color.GreenYellow);

                    Vector3 pos_c = Vector3.Transform(Vector3.Zero, bone.transform);
                    RenderTools.drawCube(pos_c, .085f);

                    // if swing bones then draw swing radius
                    /*if(vbn.swingBones.bones.Count > 0)
                    {
                        SB.SBEntry sb = null;
                        vbn.swingBones.bones.TryGetValue(bone.boneId, out sb);
                        if (sb != null)
                        {
                            // draw
                            if (bone.parentIndex != 0x0FFFFFFF && bone.parentIndex != -1)
                            {
                                int i = bone.parentIndex;
                                float degtorad = (float)(Math.PI / 180);
                                Vector3 pos_sb = Vector3.Transform(Vector3.Zero,
                                    Matrix4.CreateTranslation(new Vector3(3, 3, 3))
                                    * Matrix4.CreateScale(bone.sca)
                                    * Matrix4.CreateFromQuaternion(VBN.FromEulerAngles(sb.rx1 * degtorad, sb.ry1 * degtorad, sb.rz1 * degtorad))
                                    * Matrix4.CreateTranslation(bone.pos)
                                    * vbn.bones[i].transform);

                                Vector3 pos_sb2 = Vector3.Transform(Vector3.Zero,
                                    Matrix4.CreateTranslation(new Vector3(3, 3, 3))
                                    * Matrix4.CreateScale(bone.sca)
                                    * Matrix4.CreateFromQuaternion(VBN.FromEulerAngles(sb.rx2 * degtorad, sb.ry2 * degtorad, sb.rz2 * degtorad))
                                    * Matrix4.CreateTranslation(bone.pos)
                                    * vbn.bones[i].transform);

                                GL.Color3(Color.ForestGreen);
                                GL.Begin(PrimitiveType.LineLoop);
                                GL.Vertex3(pos_c);
                                GL.Vertex3(pos_sb);
                                GL.Vertex3(pos_sb2);
                                GL.End();
                            }
                        }
                    }*/

                    // now draw line between parent 
                    GL.Color3(Color.LightBlue);
                    GL.LineWidth(2f);

                    GL.Begin(PrimitiveType.Lines);
                    if (bone.parentIndex != 0x0FFFFFFF && bone.parentIndex != -1)
                    {
                        int i = bone.parentIndex;
                        Vector3 pos_p = Vector3.Transform(Vector3.Zero, vbn.bones[i].transform);
                        GL.Vertex3(pos_c);
                        GL.Color3(Color.Blue);
                        GL.Vertex3(pos_p);
                    }
                    GL.End();
                }
            }
        }

        private static Color getLinkColor(DAT.COLL_DATA.Link link)
        {
            if ((link.flags & 1) != 0)
                return Color.FromArgb(128, Color.Yellow);
            if ((link.collisionAngle & 4) + (link.collisionAngle & 8) != 0)
                return Color.FromArgb(128, Color.Lime);
            if ((link.collisionAngle & 2) != 0)
                return Color.FromArgb(128, Color.Red);

            return Color.FromArgb(128, Color.DarkCyan);
        }

        public static Color invertColor(Color color)
        {
            return Color.FromArgb(color.A, 255 - color.R, 255 - color.G, 255 - color.B);
        }

        public void DrawSpawn(Point s, bool isRespawn)
        {
            GL.Color4(Color.FromArgb(100, Color.Blue));
            GL.Begin(PrimitiveType.QuadStrip);
            GL.Vertex3(s.x - 3f, s.y, 0f);
            GL.Vertex3(s.x + 3f, s.y, 0f);
            GL.Vertex3(s.x - 3f, s.y + 10f, 0f);
            GL.Vertex3(s.x + 3f, s.y + 10f, 0f);
            GL.End();

            //Draw respawn platform
            if (isRespawn)
            {
                GL.Color4(Color.FromArgb(200, Color.Gray));
                GL.Begin(PrimitiveType.Triangles);
                GL.Vertex3(s.x - 5, s.y, 0);
                GL.Vertex3(s.x + 5, s.y, 0);
                GL.Vertex3(s.x, s.y, 5);

                GL.Vertex3(s.x - 5, s.y, 0);
                GL.Vertex3(s.x + 5, s.y, 0);
                GL.Vertex3(s.x, s.y, -5);

                GL.Vertex3(s.x - 5, s.y, 0);
                GL.Vertex3(s.x, s.y - 5, 0);
                GL.Vertex3(s.x, s.y, 5);

                GL.Vertex3(s.x + 5, s.y, 0);
                GL.Vertex3(s.x, s.y - 5, 0);
                GL.Vertex3(s.x, s.y, -5);

                GL.Vertex3(s.x + 5, s.y, 0);
                GL.Vertex3(s.x, s.y - 5, 0);
                GL.Vertex3(s.x, s.y, 5);

                GL.Vertex3(s.x - 5, s.y, 0);
                GL.Vertex3(s.x, s.y - 5, 0);
                GL.Vertex3(s.x, s.y, -5);
                GL.End();

                GL.Color4(Color.FromArgb(200, Color.Black));
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex3(s.x - 5, s.y, 0);
                GL.Vertex3(s.x, s.y - 5, 0);
                GL.Vertex3(s.x + 5, s.y, 0);
                GL.Vertex3(s.x, s.y - 5, 0);

                GL.Vertex3(s.x, s.y, -5);
                GL.Vertex3(s.x, s.y - 5, 0);
                GL.Vertex3(s.x, s.y, 5);
                GL.Vertex3(s.x, s.y - 5, 0);

                GL.Vertex3(s.x, s.y, -5);
                GL.Vertex3(s.x + 5, s.y, 0);
                GL.Vertex3(s.x, s.y, -5);
                GL.Vertex3(s.x - 5, s.y, 0);

                GL.Vertex3(s.x, s.y, 5);
                GL.Vertex3(s.x + 5, s.y, 0);
                GL.Vertex3(s.x, s.y, 5);
                GL.Vertex3(s.x - 5, s.y, 0);

                GL.End();
            }
        }

        public void DrawLVD()
        {
            foreach (ModelContainer m in Runtime.ModelContainers)
            {
                // JAM FIIIIIIXXXXXED IIIIIIIT
                if (m.dat_melee != null && m.dat_melee.collisions != null)
                {
                    List<int> ledges = new List<int>();
                    foreach (DAT.COLL_DATA.Link link in m.dat_melee.collisions.links)
                    {
                        GL.Begin(PrimitiveType.Quads);
                        GL.Color4(getLinkColor(link));
                        Vector2D vi = m.dat_melee.collisions.vertices[link.vertexIndices[0]];
                        GL.Vertex3(vi.x, vi.y, 5);
                        GL.Vertex3(vi.x, vi.y, -5);
                        vi = m.dat_melee.collisions.vertices[link.vertexIndices[1]];
                        GL.Vertex3(vi.x, vi.y, -5);
                        GL.Vertex3(vi.x, vi.y, 5);
                        GL.End();
                        if ((link.flags & 2) != 0)
                        {
                            ledges.Add(link.vertexIndices[0]);
                            ledges.Add(link.vertexIndices[1]);
                        }
                    }
                    GL.LineWidth(4);
                    for (int i = 0; i < m.dat_melee.collisions.vertices.Count; i++)
                    {
                        Vector2D vi = m.dat_melee.collisions.vertices[i];
                        if (ledges.Contains(i))
                            GL.Color3(Color.Purple);
                        else
                            GL.Color3(Color.Tomato);
                        GL.Begin(PrimitiveType.Lines);
                        GL.Vertex3(vi.x, vi.y, 5);
                        GL.Vertex3(vi.x, vi.y, -5);
                        GL.End();
                    }
                }

                if(m.dat_melee != null && m.dat_melee.blastzones != null)
                {
                    Bounds b = m.dat_melee.blastzones;
                    GL.Color3(Color.Red);
                    GL.Begin(PrimitiveType.LineLoop);
                    GL.Vertex3(b.left, b.top, 0);
                    GL.Vertex3(b.right, b.top, 0);
                    GL.Vertex3(b.right, b.bottom, 0);
                    GL.Vertex3(b.left, b.bottom, 0);
                    GL.End();
                }

                if (m.dat_melee != null && m.dat_melee.cameraBounds != null)
                {
                    Bounds b = m.dat_melee.cameraBounds;
                    GL.Color3(Color.Blue);
                    GL.Begin(PrimitiveType.LineLoop);
                    GL.Vertex3(b.left, b.top, 0);
                    GL.Vertex3(b.right, b.top, 0);
                    GL.Vertex3(b.right, b.bottom, 0);
                    GL.Vertex3(b.left, b.bottom, 0);
                    GL.End();
                }

                if (m.dat_melee != null && m.dat_melee.respawns != null)
                    foreach (Vector3 r in m.dat_melee.respawns)
                        DrawSpawn(new Point() { x = r.X, y = r.Y }, true);

                if (m.dat_melee != null && m.dat_melee.spawns != null)
                    foreach (Vector3 r in m.dat_melee.spawns)
                        DrawSpawn(new Point() { x = r.X, y = r.Y }, false);

                GL.Color4(Color.FromArgb(200, Color.Fuchsia));
                if (m.dat_melee != null && m.dat_melee.itemSpawns != null)
                    foreach (Vector3 r in m.dat_melee.itemSpawns)
                        RenderTools.drawCubeWireframe(r, 3);
            }

            if (Runtime.TargetLVD != null)
            {
                if (Runtime.renderCollisions)
                {
                    Vector2D vi;
                    Color color;
                    GL.LineWidth(4);
                    Matrix4 transform = Matrix4.Identity;
                    foreach (Collision c in Runtime.TargetLVD.collisions)
                    {
                        bool colSelected = (Runtime.LVDSelection == c);
                        float addX = 0, addY = 0, addZ = 0;
                        if (c.useStartPos)
                        {
                            addX = c.startPos[0];
                            addY = c.startPos[1];
                            addZ = c.startPos[2];
                        }
                        if (c.flag2)
                        {
                            //Flag2 == rigged collision
                            ModelContainer riggedModel = null;
                            Bone riggedBone = null;
                            foreach (ModelContainer m in Runtime.ModelContainers)
                            {
                                if (m.name.Equals(c.subname))
                                {
                                    riggedModel = m;
                                    if (m.vbn != null)
                                    {
                                        foreach(Bone b in m.vbn.bones)
                                        {
                                            if (b.boneName.Equals(c.unk4))
                                            {
                                                riggedBone = b;
                                            }
                                        }
                                    }
                                }
                            }
                            if(riggedModel != null)
                            {
                                if(riggedBone == null && riggedModel.vbn != null && riggedModel.vbn.bones.Count > 0)
                                {
                                    riggedBone = riggedModel.vbn.bones[0];
                                }
                                if (riggedBone != null)
                                    transform = riggedBone.invert * riggedBone.transform;
                            }
                        }

                        for(int i = 0; i < c.verts.Count - 1; i++)
                        {
                            Vector3 v1Pos = Vector3.Transform(new Vector3(c.verts[i].x + addX, c.verts[i].y + addY, addZ + 5), transform);
                            Vector3 v1Neg = Vector3.Transform(new Vector3(c.verts[i].x + addX, c.verts[i].y + addY, addZ - 5), transform);
                            Vector3 v1Zero = Vector3.Transform(new Vector3(c.verts[i].x + addX, c.verts[i].y + addY, addZ), transform);
                            Vector3 v2Pos = Vector3.Transform(new Vector3(c.verts[i + 1].x + addX, c.verts[i + 1].y + addY, addZ + 5), transform);
                            Vector3 v2Neg = Vector3.Transform(new Vector3(c.verts[i + 1].x + addX, c.verts[i + 1].y + addY, addZ - 5), transform);
                            Vector3 v2Zero = Vector3.Transform(new Vector3(c.verts[i + 1].x + addX, c.verts[i + 1].y + addY, addZ), transform);

                            GL.Begin(PrimitiveType.Quads);
                            if(c.normals.Count > i)
                            {
                                if (Runtime.renderCollisionNormals)
                                {
                                    Vector3 v = Vector3.Add(Vector3.Divide(Vector3.Subtract(v1Zero, v2Zero), 2), v2Zero);
                                    GL.End();
                                    GL.Begin(PrimitiveType.Lines);
                                    GL.Color3(Color.Blue);
                                    GL.Vertex3(v);
                                    GL.Vertex3(v.X + (c.normals[i].x * 5), v.Y + (c.normals[i].y * 5), v.Z);
                                    GL.End();
                                    GL.Begin(PrimitiveType.Quads);
                                }
                                
				                if(c.flag4)
				                    color = Color.FromArgb(128, Color.Yellow);
                                else if (Math.Abs(c.normals[i].x) > Math.Abs(c.normals[i].y))
                                    color = Color.FromArgb(128, Color.Lime);
                                else if(c.normals[i].y < 0)
                                    color = Color.FromArgb(128, Color.Red);
                                else
				                    color = Color.FromArgb(128, Color.Cyan);

                                if ((colSelected || Runtime.LVDSelection == c.normals[i]) && ((int)(DateTime.Now.Millisecond / 500) == 0))
                                    color = invertColor(color);

                                GL.Color4(color);
                            }
                            else
                            {
                                GL.Color4(Color.FromArgb(128, Color.Gray));
                            }
                            GL.Vertex3(v1Pos);
                            GL.Vertex3(v1Neg);
                            GL.Vertex3(v2Neg);
                            GL.Vertex3(v2Pos);
                            GL.End();

                            GL.Begin(PrimitiveType.Lines);
                            if (c.materials.Count > i)
                            {
                                if (c.materials[i].getFlag(6) || (i > 0 && c.materials[i - 1].getFlag(7)))
                                    color = Color.Purple;
                                else
                                    color = Color.Orange;

                                if ((colSelected || Runtime.LVDSelection == c.verts[i]) && ((int)(DateTime.Now.Millisecond / 500) == 0))
                                    color = invertColor(color);
                                GL.Color4(color);
                            }
                            else
                            {
                                GL.Color4(Color.Gray);
                            }
                            GL.Vertex3(v1Pos);
                            GL.Vertex3(v1Neg);

                            if (i == c.verts.Count - 2)
                            {
                                if (c.materials.Count > i)
                                {
                                    if (c.materials[i].getFlag(7))
                                        color = Color.Purple;
                                    else
                                        color = Color.Orange;

                                    if (Runtime.LVDSelection == c.verts[i+1] && ((int)(DateTime.Now.Millisecond / 500) == 0))
                                        color = invertColor(color);
                                    GL.Color4(color);
                                }
                                else
                                {
                                    GL.Color4(Color.Gray);
                                }
                                GL.Vertex3(v2Pos);
                                GL.Vertex3(v2Neg);
                            }
                            GL.End();
                        }
                    }
                }

                if (Runtime.renderItemSpawners)
                {
                    foreach (ItemSpawner c in Runtime.TargetLVD.items)
                    {
                        foreach (Section s in c.sections)
                        {
                            // draw the item spawn quads
                            GL.LineWidth(2);

                            // draw outside borders
                            GL.Color3(Color.Black);
                            GL.Begin(PrimitiveType.LineStrip);
                            foreach (Vector2D vi in s.points)
                            {
                                GL.Vertex3(vi.x, vi.y, 5);
                            }
                            GL.End();
                            GL.Begin(PrimitiveType.LineStrip);
                            foreach (Vector2D vi in s.points)
                            {
                                GL.Vertex3(vi.x, vi.y, -5);
                            }
                            GL.End();


                            // draw vertices
                            GL.Color3(Color.White);
                            GL.Begin(PrimitiveType.Lines);
                            foreach (Vector2D vi in s.points)
                            {
                                GL.Vertex3(vi.x, vi.y, 5);
                                GL.Vertex3(vi.x, vi.y, -5);
                            }
                            GL.End();
                        }
                    }
                }

                if (Runtime.renderSpawns)
                {
                    foreach (Point s in Runtime.TargetLVD.spawns)
                    {
                        DrawSpawn(s, false);
                    }
                }

                if (Runtime.renderRespawns)
                {
                    foreach (Point s in Runtime.TargetLVD.respawns)
                    {
                        DrawSpawn(s,true);
                    }
                }

                if (Runtime.renderGeneralPoints)
                {
                    foreach (Point g in Runtime.TargetLVD.generalPoints)
                    {
                        GL.Color4(Color.FromArgb(200, Color.Fuchsia));
                        RenderTools.drawCubeWireframe(new Vector3(g.x, g.y, 0), 3);
                    }
                    
                    foreach (LVDGeneralShape shape in Runtime.TargetLVD.generalShapes)
                    {
                        if(shape is GeneralPoint)
                        {
                            GeneralPoint g = (GeneralPoint)shape;
                            GL.Color4(Color.FromArgb(200, Color.Fuchsia));
                            RenderTools.drawCubeWireframe(new Vector3(g.x, g.y, 0), 3);
                        }
                        if(shape is GeneralRect)
                        {
                            GeneralRect b = (GeneralRect)shape;
                            GL.Color4(Color.FromArgb(200, Color.Fuchsia));
                            GL.Begin(PrimitiveType.LineLoop);
                            GL.Vertex3(b.x1, b.y1, 0);
                            GL.Vertex3(b.x2, b.y1, 0);
                            GL.Vertex3(b.x2, b.y2, 0);
                            GL.Vertex3(b.x1, b.y2, 0);
                            GL.End();
                        }
                        if(shape is GeneralPath)
                        {
                            List<Vector2D> p = ((GeneralPath)shape).points;
                            GL.Color4(Color.FromArgb(200, Color.Fuchsia));
                            GL.Begin(PrimitiveType.LineStrip);
                            foreach(Vector2D point in p)
                                GL.Vertex3(point.x, point.y, 0);
                            GL.End();
                        }
                    }
                }

                if (Runtime.renderOtherLVDEntries)
                {
                    GL.Color4(Color.FromArgb(128, Color.Yellow));
                    foreach (Sphere s in Runtime.TargetLVD.damageSpheres)
                    {
                        RenderTools.drawSphere(new Vector3(s.x, s.y, s.z), s.radius, 24);
                    }

                    foreach (Capsule c in Runtime.TargetLVD.damageCapsules)
                    {
                        RenderTools.drawCylinder(new Vector3(c.x, c.y, c.z), new Vector3(c.x + c.dx, c.y + c.dy, c.z + c.dz), c.r);
                    }

                    GL.Color4(Color.FromArgb(128, Color.Red));
                    foreach (Bounds b in Runtime.TargetLVD.blastzones)
                    {
                        GL.Begin(PrimitiveType.LineLoop);
                        GL.Vertex3(b.left, b.top, 0);
                        GL.Vertex3(b.right, b.top, 0);
                        GL.Vertex3(b.right, b.bottom, 0);
                        GL.Vertex3(b.left, b.bottom, 0);
                        GL.End();
                    }

                    GL.Color4(Color.FromArgb(128, Color.Blue));
                    foreach (Bounds b in Runtime.TargetLVD.cameraBounds)
                    {
                        GL.Begin(PrimitiveType.LineLoop);
                        GL.Vertex3(b.left, b.top, 0);
                        GL.Vertex3(b.right, b.top, 0);
                        GL.Vertex3(b.right, b.bottom, 0);
                        GL.Vertex3(b.left, b.bottom, 0);
                        GL.End();
                    }
                }
            }
        }

        private void DrawPathDisplay()
        {
            if (Runtime.TargetPath != null && !checkBox1.Checked)
            {
                GL.Color3(Color.Yellow);
                GL.LineWidth(2);
                for (int i = 1; i < Runtime.TargetPath.Frames.Count; i++)
                {
                    GL.Begin(PrimitiveType.Lines);
                    GL.Vertex3(Runtime.TargetPath.Frames[i].x, Runtime.TargetPath.Frames[i].y, Runtime.TargetPath.Frames[i].z);
                    GL.Vertex3(Runtime.TargetPath.Frames[i - 1].x, Runtime.TargetPath.Frames[i - 1].y, Runtime.TargetPath.Frames[i - 1].z);
                    GL.End();
                }
            }

            if (Runtime.TargetCMR0 != null && !checkBox1.Checked)
            {
                GL.Color3(Color.Yellow);
                GL.LineWidth(2);
                for (int i = 1; i < Runtime.TargetCMR0.frames.Count; i++)
                {
                    GL.Begin(PrimitiveType.Lines);
                    GL.Vertex3(Runtime.TargetCMR0.frames[i].M14, Runtime.TargetCMR0.frames[i].M24, Runtime.TargetCMR0.frames[i].M34);
                    GL.Vertex3(Runtime.TargetCMR0.frames[i - 1].M14, Runtime.TargetCMR0.frames[i - 1].M24, Runtime.TargetCMR0.frames[i - 1].M34);
                    GL.End();
                }
            }
        }

        public void RenderHitboxes()
        {
            if (Hitboxes.Count > 0)
            {
                GL.Color4(Color.FromArgb(85, Color.Red));
                GL.Enable(EnableCap.DepthTest);
                GL.Enable(EnableCap.Blend);

                foreach (var pair in Hitboxes)
                {
                    var h = pair.Value;
                    var va = new Vector3(h.X, h.Y, h.Z);

                    int bid = h.Bone;
                    int gr = 0;
                    if (bid > 1000)
                    {
                        /*while (bid >= 1000)
                        {
                            bid -= 1000;
                            gr++;
                        }
                        gr = gr % 2;
                        //bid = bid >> 6;
                        Console.WriteLine(h.Bone + " " + gr + " " + bid);*/
                        bid >>= 8;
                    }

                    Bone b = new Bone();

                    if (h.Bone != -1)
                    {
                        foreach (ModelContainer m in Runtime.ModelContainers)
                        {
                            if (m.vbn != null)
                            {
                                if (m.vbn.jointTable.Count < 1)
                                    b = m.vbn.bones[bid];
                                else
                                {
                                    b = m.vbn.bones[m.vbn.jointTable[gr][bid]];
                                }
                            }
                        }
                    }

                    va = Vector3.Transform(va, b.transform.ClearScale());


                    // Draw angle marker
                    /*GL.LineWidth(7f);
                    GL.Begin(PrimitiveType.Lines);
                    GL.Color3(Color.Black);
                    GL.Vertex3(va);
                    GL.Vertex3(va + Vector3.Transform(new Vector3(0,0,h.Size), Matrix4.CreateRotationX(-h.Angle * ((float)Math.PI / 180f))));
                    GL.End();*/

                    GL.Color4(Color.FromArgb(85, Color.Red));

                    switch (h.Type)
                    {
                        case Hitbox.HITBOX:
                            GL.Color4(Color.FromArgb(85, Color.Red));
                            break;
                        case Hitbox.GRABBOX:
                            GL.Color4(Color.FromArgb(85, Color.Purple));
                            break;
                        case Hitbox.WINDBOX:
                            GL.Color4(Color.FromArgb(85, Color.Blue));
                            break;
                    }

                    GL.DepthMask(false);
                    if (h.Extended)
                    {
                        var va2 = new Vector3(h.X2, h.Y2, h.Z2);

                        if (h.Bone != -1)
                            va2 = Vector3.Transform(va2, b.transform.ClearScale());

                        RenderTools.drawCylinder(va, va2, h.Size);
                    }
                    else
                    {
                        RenderTools.drawSphere(va, h.Size, 30);
                    }
                }

                GL.Disable(EnableCap.Blend);
                GL.Disable(EnableCap.DepthTest);
            }
        }

        ACMDScript script;

        public void ProcessFrame()
        {
            Hitboxes.Clear();
            int halt = 0;
            int e = 0;
            int setLoop = 0;
            int iterations = 0;
            var cmd = script[e];

            while (halt < Frame)
            {
                switch (cmd.Ident)
                {
                    case 0x42ACFE7D: // Asynchronous Timer
                        {
                            halt = (int)(float)cmd.Parameters[0] - 2;
                            break;
                        }
                    case 0x4B7B6E51: // Synchronous Timer
                        {
                            halt += (int)(float)cmd.Parameters[0];
                            break;
                        }
                    case 0xB738EABD: // hitbox 
                        {
                            Hitbox h = new Hitbox();
                            int id = (int)cmd.Parameters[0];
                            if (Hitboxes.ContainsKey(id))
                                Hitboxes.Remove(id);
                            h.Type = Hitbox.HITBOX;
                            h.Bone = ((int)cmd.Parameters[2] - 1).Clamp(0, int.MaxValue);
                            h.Damage = (float)cmd.Parameters[3];
                            h.Angle = (int)cmd.Parameters[4];
                            h.KnockbackGrowth = (int)cmd.Parameters[5];
                            //FKB = (float)cmd.Parameters[6]
                            h.KnockbackBase = (int)cmd.Parameters[7];
                            h.Size = (float)cmd.Parameters[8];
                            h.X = (float)cmd.Parameters[9];
                            h.Y = (float)cmd.Parameters[10];
                            h.Z = (float)cmd.Parameters[11];
                            Hitboxes.Add(id, h);
                            break;
                        }
                    case 0x2988D50F: // Extended hitbox
                        {
                            Hitbox h = new Hitbox();
                            int id = (int)cmd.Parameters[0];
                            if (Hitboxes.ContainsKey(id))
                                Hitboxes.Remove(id);
                            h.Type = Hitbox.HITBOX;
                            h.Extended = true;
                            h.Bone = ((int)cmd.Parameters[2] - 1).Clamp(0, int.MaxValue);
                            h.Damage = (float)cmd.Parameters[3];
                            h.Angle = (int)cmd.Parameters[4];
                            h.KnockbackGrowth = (int)cmd.Parameters[5];
                            //FKB = (float)cmd.Parameters[6]
                            h.KnockbackBase = (int)cmd.Parameters[7];
                            h.Size = (float)cmd.Parameters[8];
                            h.X = (float)cmd.Parameters[9];
                            h.Y = (float)cmd.Parameters[10];
                            h.Z = (float)cmd.Parameters[11];
                            h.X2 = (float)cmd.Parameters[24];
                            h.Y2 = (float)cmd.Parameters[25];
                            h.Z2 = (float)cmd.Parameters[26];
                            Hitboxes.Add(id, h);
                            break;
                        }
                    case 0x14FCC7E4: // special hitbox
                        {
                            Hitbox h = new Hitbox();
                            int id = (int)cmd.Parameters[0];
                            if (Hitboxes.ContainsKey(id))
                                Hitboxes.Remove(id);
                            h.Type = Hitbox.HITBOX;
                            h.Bone = ((int)cmd.Parameters[2] - 1).Clamp(0, int.MaxValue);
                            h.Damage = (float)cmd.Parameters[3];
                            h.Angle = (int)cmd.Parameters[4];
                            h.KnockbackGrowth = (int)cmd.Parameters[5];
                            //FKB = (float)cmd.Parameters[6]
                            h.KnockbackBase = (int)cmd.Parameters[7];
                            h.Size = (float)cmd.Parameters[8];
                            h.X = (float)cmd.Parameters[9];
                            h.Y = (float)cmd.Parameters[10];
                            h.Z = (float)cmd.Parameters[11];
                            Hitboxes.Add(id, h);
                            break;
                        }
                    case 0x7075DC5A: // Extended special hitbox
                        {
                            Hitbox h = new Hitbox();
                            int id = (int)cmd.Parameters[0];
                            if (Hitboxes.ContainsKey(id))
                                Hitboxes.Remove(id);
                            h.Type = Hitbox.HITBOX;
                            h.Extended = true;
                            h.Bone = ((int)cmd.Parameters[2] - 1).Clamp(0, int.MaxValue);
                            h.Damage = (float)cmd.Parameters[3];
                            h.Angle = (int)cmd.Parameters[4];
                            h.KnockbackGrowth = (int)cmd.Parameters[5];
                            //FKB = (float)cmd.Parameters[6]
                            h.KnockbackBase = (int)cmd.Parameters[7];
                            h.Size = (float)cmd.Parameters[8];
                            h.X = (float)cmd.Parameters[9];
                            h.Y = (float)cmd.Parameters[10];
                            h.Z = (float)cmd.Parameters[11];
                            h.X2 = (float)cmd.Parameters[40];
                            h.Y2 = (float)cmd.Parameters[41];
                            h.Z2 = (float)cmd.Parameters[42];
                            Hitboxes.Add(id, h);
                            break;
                        }
                    case 0x9245E1A8: // clear all hitboxes
                        Hitboxes.Clear();
                        break;
                    case 0xFF379EB6: // delete hitbox
                        if (Hitboxes.ContainsKey((int)cmd.Parameters[0]))
                        {
                            Hitboxes.Remove((int)cmd.Parameters[0]);
                        }
                        break;
                    case 0x7698BB42: // deactivate previous hitbox
                        Hitboxes.Remove(Hitboxes.Keys.Max());
                        break;
                    case 0xEB375E3: // Set Loop
                        iterations = int.Parse(cmd.Parameters[0] + "") - 1;
                        setLoop = e;
                        break;
                    case 0x38A3EC78: // goto
                        if (iterations > 0)
                        {
                            e = setLoop;
                            iterations -= 1;
                        }
                        break;

                    case 0x7B48FE1C: // grabbox 
                        {
                            Hitbox h = new Hitbox();
                            int id = (int)cmd.Parameters[0];
                            h.Type = Hitbox.GRABBOX;
                            h.Bone = (int.Parse(cmd.Parameters[1] + "") - 1).Clamp(0, int.MaxValue);
                            h.Size = (float)cmd.Parameters[2];
                            h.X = (float)cmd.Parameters[3];
                            h.Y = (float)cmd.Parameters[4];
                            h.Z = (float)cmd.Parameters[5];

                            if (cmd.Parameters.Count > 8)
                            {
                                h.X2 = float.Parse(cmd.Parameters[8] + "");
                                h.Y2 = float.Parse(cmd.Parameters[9] + "");
                                h.Z2 = float.Parse(cmd.Parameters[10] + "");
                            }

                            Hitboxes.Add(id, h);
                            break;
                        }
                    case 0xF3A464AC: // Terminate_Grab_Collisions
                        List<Hitbox> toDelete = new List<Hitbox>();
                        for (int i = 0; i < Hitboxes.Count; i++)
                        {
                            if (Hitboxes.Values[i].Type == Hitbox.GRABBOX)
                                toDelete.Add(Hitboxes.Values[i]);
                        }
                        foreach (Hitbox h in toDelete)
                            Hitboxes.Remove(Hitboxes.IndexOfValue(h));
                        break;
                    case 0xFAA85333:
                        break;
                    case 0x321297B0:
                        break;
                    case 0xED67D5DA:
                        break;
                    case 0x7640AEEB:
                        break;

                }

                e++;
                if (e >= script.Count)
                    break;
                else
                    cmd = script[e];

                if (halt > Frame)
                    break;
            }
        }

        public void HandleACMD(string animname)
        {
            //Console.WriteLine("Handling " + animname);
            var crc = Crc32.Compute(animname.Replace(".omo", "").ToLower());

            if (Runtime.Moveset == null)
            {
                script = null;
                return;
            }

            if (!Runtime.Moveset.Game.Scripts.ContainsKey(crc))
            {
                script = null;
                return;
            }

            //Console.WriteLine("Handling " + animname);
            script = (ACMDScript)Runtime.Moveset.Game.Scripts[crc];
            if(Runtime.acmdEditor.crc != crc)
                Runtime.acmdEditor.SetAnimation(crc);
        }

        #endregion

        public void SetFrame(int frame)
        {
            Runtime.TargetAnim.setFrame(frame);
            foreach (ModelContainer m in Runtime.ModelContainers)
            {
                if (m.vbn != null)
                    Runtime.TargetAnim.nextFrame(m.vbn);

                if (m.dat_melee != null)
                {
                    Runtime.TargetAnim.setFrame((int)this.nupdFrame.Value);
                    Runtime.TargetAnim.nextFrame(m.dat_melee.bones);
                }
            }
        }
        public void loadAnimation(SkelAnimation a)
        {
            a.setFrame(0);
            foreach (ModelContainer m in Runtime.ModelContainers)
            {
                if (m.vbn != null)
                    Runtime.TargetAnim.nextFrame(m.vbn);
            }
            nupdMaxFrame.Value = a.size() > 1 ? a.size() - 1 : a.size();
            nupdFrame.Value = 0;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            rot = 0;
            lookup = 0;
            height = 0;
            width = 0;
            zoom = 0;
            nzoom = 0;
            mouseXLast = 0;
            mouseYLast = 0;
            mouseSLast = 0;
            UpdateMousePosition();
        }

        public void loadMTA(MTA m)
        {
            Console.WriteLine("MTA Loaded");
            Frame = 0;
            nupdFrame.Value = 0;
            nupdMaxFrame.Value = m.numFrames;
            //Console.WriteLine(m.numFrames);
            Runtime.TargetMTA = m;
        }

        private void VBNViewport_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            if (e.KeyChar == 'f')
            {
                fpsView = !fpsView;

                // I need to convert the camera stuff...
                if (fpsView)
                {
                    //Console.WriteLine(trans);
                    /*Quaternion rot = v.ExtractRotation();
                    Vector3 tr = Vector3.Transform(Vector3.Zero, v.Inverted());
                    width = tr.X / 5f;
                    height = (tr.Y+5f) / -5f;
                    zoom = -(tr.Z + 15f);*/
                }
                else
                {
                }
            }
        }

        private void glControl1_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            /*//create ray for mouse
            //normalize mouse in 3d space
            Vector4 ray = new Vector4(
                (2.0f * e.X) / glControl1.Width - 1.0f,
                1.0f - (2.0f * e.Y) / glControl1.Height,
                -1.0f,
                1.0f);
            ray = Vector4.Transform(ray, v.Inverted());
            ray = new Vector4(ray.X,ray.Y,-1.0f, 1.0f);
            ray.Normalize();
            Console.WriteLine(ray.ToString());
            int t = 0;
            while(t < 20)
            {

                Vector4 b = ray * t * new Vector4((new Vector3(0, 0, 0) - new Vector3(2, 2, 2)), 1);
                Vector4 c = (new Vector4((new Vector3(0, 0, 0) - new Vector3(2, 2, 2)), 1))
                    * new Vector4((new Vector3(0, 0, 0) - new Vector3(2, 2, 2)), 1)
                    - new Vector4(16, 16, 16, 16);
                Console.WriteLine(b * b - c);
                t++;
            }*/

        }

        public void FPSCamera()
        {
            if (OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.A))
                x += 0.2f;
            if (OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.D))
                x -= 0.2f;
            if (OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.W))
                zoom += 0.2f;
            if (OpenTK.Input.Keyboard.GetState().IsKeyDown(OpenTK.Input.Key.S))
                zoom -= 0.2f;

            rot += 0.025f * (OpenTK.Input.Mouse.GetState().X - mouseXLast);
            lookup += 0.025f * (OpenTK.Input.Mouse.GetState().Y - mouseYLast);

            mouseXLast = OpenTK.Input.Mouse.GetState().X;
            mouseYLast = OpenTK.Input.Mouse.GetState().Y;

            v = Matrix4.CreateTranslation(5 * width, -5f - 5f * height, -15f + zoom) * Matrix4.CreateRotationY(0.5f * rot) * Matrix4.CreateRotationX(0.2f * lookup) 
                * Matrix4.CreatePerspectiveFieldOfView(1.3f, Width / (float)Height, 1.0f, Runtime.renderDepth);
        }
    }
}
