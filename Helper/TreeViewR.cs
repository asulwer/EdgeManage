using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

/*
 * All of this TreeView Drag-n-Drop code is from: http://www.codeproject.com/Articles/6184/TreeView-Rearrange
 * with added code from: https://www.fmsinc.com/free/NewTips/NET/NETtip21.asp
 * and from: http://stackoverflow.com/questions/1709581/whilst-using-drag-and-drop-can-i-cause-a-treeview-to-expand-the-node-over-which
 */

namespace EdgeManage.Helper
{
    partial class TreeViewR : System.Windows.Forms.TreeView
    {
        #region Changes
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, UInt32 wMsg, Int32 wParam, Int32 lParam);

        TreeNode lastDragDestination = null;
        DateTime lastDragDestinationTime;

        private List<string> pfvStickNodes = new List<string>();

        public List<string> StickyNodes
        {
            get { return pfvStickNodes; }
            set { pfvStickNodes = value; }
        }
        #endregion

        private string NodeMap;
        private const int MAPSIZE = 128;
        private StringBuilder NewNodeMap = new StringBuilder(MAPSIZE);

        public TreeViewR() : base()
        {
            this.AllowDrop = true;

            this.ItemHeight = this.ItemHeight + 3;
            this.Indent = this.Indent + 3;

            // wire up our local event handlers
            this.ItemDrag += new ItemDragEventHandler(tvrItemDrag);
            this.DragEnter += new DragEventHandler(tvrDragEnter);
            this.DragDrop += new DragEventHandler(tvrDragDrop);
            this.DragOver += new DragEventHandler(tvrDragOver);
            this.MouseDown += new MouseEventHandler(tvrMouseDown);
        }

        private void tvrMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            this.SelectedNode = this.GetNodeAt(e.X, e.Y);
        }

        protected void tvrItemDrag(object sender, System.Windows.Forms.ItemDragEventArgs e)
        {
            DoDragDrop(e.Item, DragDropEffects.Move);
        }

        protected void tvrDragEnter(object sender, System.Windows.Forms.DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        protected void tvrDragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", false) && this.NodeMap != "")
            {
                TreeNode MovingNode = (TreeNode)e.Data.GetData("System.Windows.Forms.TreeNode");
                string[] NodeIndexes = this.NodeMap.Split('|');
                TreeNodeCollection InsertCollection = this.Nodes;
                for (int i = 0; i < NodeIndexes.Length - 1; i++)
                {
                    InsertCollection = InsertCollection[Int32.Parse(NodeIndexes[i])].Nodes;
                }

                if (InsertCollection != null)
                {
                    InsertCollection.Insert(Int32.Parse(NodeIndexes[NodeIndexes.Length - 1]), (TreeNode)MovingNode.Clone());
                    this.SelectedNode = InsertCollection[Int32.Parse(NodeIndexes[NodeIndexes.Length - 1])];
                    MovingNode.Remove();
                }
            }
        }

        private void tvrDragOver(object sender, System.Windows.Forms.DragEventArgs e)
        {
            TreeNode NodeOver = this.GetNodeAt(this.PointToClient(Cursor.Position));
            TreeNode NodeMoving = (TreeNode)e.Data.GetData("System.Windows.Forms.TreeNode");

            // A bit long, but to summarize, process the following code only if the nodeover is null
            // and either the nodeover is not the same thing as nodemoving UNLESSS nodeover happens
            // to be the last node in the branch (so we can allow drag & drop below a parent branch)
            if (NodeOver != null && (NodeOver != NodeMoving || (NodeOver.Parent != null && NodeOver.Index == (NodeOver.Parent.Nodes.Count - 1))))
            {
                #region Changes

                // Set a constant to define the autoscroll region
                const Single scrollRegion = 20;

                // See where the cursor is
                Point pt = this.PointToClient(Cursor.Position);

                // See if we need to scroll up or down
                if ((pt.Y + (scrollRegion * 2)) > this.Height)
                {
                    // Call the API to scroll down
                    SendMessage(this.Handle, (int)277, (int)1, 0);
                }
                else if (pt.Y < (this.Top + scrollRegion))
                {
                    // Call the API to scroll up
                    SendMessage(this.Handle, (int)277, (int)0, 0);
                }

                //if we are on a new object, reset our timer
                //otherwise check to see if enough time has passed and expand the destination node
                if (NodeOver != lastDragDestination)
                {
                    lastDragDestination = NodeOver;
                    lastDragDestinationTime = DateTime.Now;
                }
                else
                {
                    TimeSpan hoverTime = DateTime.Now.Subtract(lastDragDestinationTime);
                    if (hoverTime.TotalSeconds > 1)
                    {
                        NodeOver.Expand();
                    }
                }

                // mark some nodes as "sticky" (so they can't be moved)
                if (StickyNodes.Contains(NodeMoving.FullPath))
                {
                    NodeMap = "";
                    return;
                }
                #endregion

                int OffsetY = this.PointToClient(Cursor.Position).Y - NodeOver.Bounds.Top;
                int NodeOverImageWidth = this.ImageList.Images[NodeOver.ImageIndex].Size.Width + 8;
                Graphics g = this.CreateGraphics();

                // Image index of 1 is the non-folder icon
                if (NodeOver.ImageIndex == 1)
                {
                    //Standard Node
                    if (OffsetY < (NodeOver.Bounds.Height / 2))
                    {
                        ///"top";

                        #region Changes
                        if (NodeOver == this.Nodes[0])
                        {
                            return;
                        }
                        #endregion

                        //If NodeOver is a child then cancel
                        TreeNode tnParadox = NodeOver;
                        while (tnParadox.Parent != null)
                        {
                            if (tnParadox.Parent == NodeMoving)
                            {
                                this.NodeMap = "";
                                return;
                            }

                            tnParadox = tnParadox.Parent;
                        }

                        //Store the placeholder info into a pipe delimited string
                        SetNewNodeMap(NodeOver, false);
                        if (SetMapsEqual() == true)
                            return;

                        //Clear placeholders above and below
                        this.Refresh();

                        // Draw the placeholders
                        this.DrawLeafTopPlaceholders(NodeOver);

                    }
                    else
                    {
                        // "bottom";

                        // If NodeOver is a child then cancel
                        TreeNode tnParadox = NodeOver;
                        while (tnParadox.Parent != null)
                        {
                            if (tnParadox.Parent == NodeMoving)
                            {
                                this.NodeMap = "";
                                return;
                            }

                            tnParadox = tnParadox.Parent;
                        }

                        //Allow drag drop to parent branches
                        TreeNode ParentDragDrop = null;
                        // If the node the mouse is over is the last node of the branch we should allow
                        // the ability to drop the "nodemoving" node BELOW the parent node
                        if (NodeOver.Parent != null && NodeOver.Index == (NodeOver.Parent.Nodes.Count - 1))
                        {
                            int XPos = this.PointToClient(Cursor.Position).X;
                            if (XPos < NodeOver.Bounds.Left)
                            {
                                ParentDragDrop = NodeOver.Parent;

                                if (XPos < (ParentDragDrop.Bounds.Left - this.ImageList.Images[ParentDragDrop.ImageIndex].Size.Width))
                                {
                                    if (ParentDragDrop.Parent != null)
                                        ParentDragDrop = ParentDragDrop.Parent;
                                }
                                #region Changes
                                // can't move it past the "top"
                                if (ParentDragDrop == this.Nodes[0])
                                {
                                    return;
                                }
                                #endregion
                            }
                        }

                        //Store the placeholder info into a pipe delimited string
                        // Since we are in a special case here, use the ParentDragDrop node as the current "nodeover"
                        SetNewNodeMap(ParentDragDrop != null ? ParentDragDrop : NodeOver, true);
                        if (SetMapsEqual() == true)
                            return;

                        // Clear placeholders above and below
                        this.Refresh();

                        //Draw the placeholders
                        DrawLeafBottomPlaceholders(NodeOver, ParentDragDrop);
                    }

                }
                else
                {
                    // Folder Node
                    if (OffsetY < (NodeOver.Bounds.Height / 3))
                    {
                        //"folder top";
                        #region Changes
                        if (NodeOver == this.Nodes[0])
                        {
                            return;
                        }
                        if (StickyNodes.Contains(NodeOver.FullPath))
                        {
                            return;
                        }
                        #endregion
                        //If NodeOver is a child then cancel
                        TreeNode tnParadox = NodeOver;
                        while (tnParadox.Parent != null)
                        {
                            if (tnParadox.Parent == NodeMoving)
                            {
                                this.NodeMap = "";
                                return;
                            }

                            tnParadox = tnParadox.Parent;
                        }

                        //Store the placeholder info into a pipe delimited string
                        SetNewNodeMap(NodeOver, false);
                        if (SetMapsEqual() == true)
                            return;

                        //Clear placeholders above and below
                        this.Refresh();

                        //Draw the placeholders
                        this.DrawFolderTopPlaceholders(NodeOver);

                    }
                    else if ((NodeOver.Parent != null && NodeOver.Index == 0) && (OffsetY > (NodeOver.Bounds.Height - (NodeOver.Bounds.Height / 3))))
                    {
                        //"folder bottom"

                        //If NodeOver is a child then cancel
                        TreeNode tnParadox = NodeOver;
                        while (tnParadox.Parent != null)
                        {
                            if (tnParadox.Parent == NodeMoving)
                            {
                                this.NodeMap = "";
                                return;
                            }

                            tnParadox = tnParadox.Parent;
                        }

                        //Store the placeholder info into a pipe delimited string
                        SetNewNodeMap(NodeOver, true);
                        if (SetMapsEqual() == true)
                            return;

                        //Clear placeholders above and below
                        this.Refresh();

                        //Draw the placeholders
                        DrawFolderTopPlaceholders(NodeOver);

                    }
                    else
                    {
                        //folder over"
                        if (NodeOver.Nodes.Count > 0)
                        {
                            #region Changes 
                            // now done via the "hover" expand feature
                            // NodeOver.Expand();
                            #endregion
                            //this.Refresh();
                        }
                        else
                        {
                            //Prevent the node from being dragged onto itself
                            if (NodeMoving == NodeOver)
                                return;

                            //If NodeOver is a child then cancel
                            TreeNode tnParadox = NodeOver;
                            while (tnParadox.Parent != null)
                            {
                                if (tnParadox.Parent == NodeMoving)
                                {
                                    this.NodeMap = "";
                                    return;
                                }
                                tnParadox = tnParadox.Parent;
                            }

                            //Store the placeholder info into a pipe delimited string
                            SetNewNodeMap(NodeOver, false);
                            NewNodeMap = NewNodeMap.Insert(NewNodeMap.Length, "|0");

                            if (SetMapsEqual() == true)
                                return;

                            //Clear placeholders above and below
                            this.Refresh();

                            //Draw the "add to folder" placeholder
                            DrawAddToFolderPlaceholder(NodeOver);
                        }
                    }

                }
            }
        }

        private void DrawLeafTopPlaceholders(TreeNode NodeOver)
        {
            Graphics g = this.CreateGraphics();

            int NodeOverImageWidth = this.ImageList.Images[NodeOver.ImageIndex].Size.Width + 8;
            int LeftPos = NodeOver.Bounds.Left - NodeOverImageWidth;
            int RightPos = this.Width - 4;

            Point[] LeftTriangle = new Point[5]{
                                                   new Point(LeftPos, NodeOver.Bounds.Top - 4),
                                                   new Point(LeftPos, NodeOver.Bounds.Top + 4),
                                                   new Point(LeftPos + 4, NodeOver.Bounds.Y),
                                                   new Point(LeftPos + 4, NodeOver.Bounds.Top - 1),
                                                   new Point(LeftPos, NodeOver.Bounds.Top - 5)};

            Point[] RightTriangle = new Point[5]{
                                                    new Point(RightPos, NodeOver.Bounds.Top - 4),
                                                    new Point(RightPos, NodeOver.Bounds.Top + 4),
                                                    new Point(RightPos - 4, NodeOver.Bounds.Y),
                                                    new Point(RightPos - 4, NodeOver.Bounds.Top - 1),
                                                    new Point(RightPos, NodeOver.Bounds.Top - 5)};


            g.FillPolygon(System.Drawing.Brushes.Black, LeftTriangle);
            g.FillPolygon(System.Drawing.Brushes.Black, RightTriangle);
            g.DrawLine(new System.Drawing.Pen(Color.Black, 2), new Point(LeftPos, NodeOver.Bounds.Top), new Point(RightPos, NodeOver.Bounds.Top));

        }

        private void DrawLeafBottomPlaceholders(TreeNode NodeOver, TreeNode ParentDragDrop)
        {
            Graphics g = this.CreateGraphics();

            int NodeOverImageWidth = this.ImageList.Images[NodeOver.ImageIndex].Size.Width + 8;
            // Once again, we are not dragging to node over, draw the placeholder using the ParentDragDrop bounds
            int LeftPos, RightPos;
            if (ParentDragDrop != null)
                LeftPos = ParentDragDrop.Bounds.Left - (this.ImageList.Images[ParentDragDrop.ImageIndex].Size.Width + 8);
            else
                LeftPos = NodeOver.Bounds.Left - NodeOverImageWidth;
            RightPos = this.Width - 4;

            Point[] LeftTriangle = new Point[5]{
                                                   new Point(LeftPos, NodeOver.Bounds.Bottom - 4),
                                                   new Point(LeftPos, NodeOver.Bounds.Bottom + 4),
                                                   new Point(LeftPos + 4, NodeOver.Bounds.Bottom),
                                                   new Point(LeftPos + 4, NodeOver.Bounds.Bottom - 1),
                                                   new Point(LeftPos, NodeOver.Bounds.Bottom - 5)};

            Point[] RightTriangle = new Point[5]{
                                                    new Point(RightPos, NodeOver.Bounds.Bottom - 4),
                                                    new Point(RightPos, NodeOver.Bounds.Bottom + 4),
                                                    new Point(RightPos - 4, NodeOver.Bounds.Bottom),
                                                    new Point(RightPos - 4, NodeOver.Bounds.Bottom - 1),
                                                    new Point(RightPos, NodeOver.Bounds.Bottom - 5)};


            g.FillPolygon(System.Drawing.Brushes.Black, LeftTriangle);
            g.FillPolygon(System.Drawing.Brushes.Black, RightTriangle);
            g.DrawLine(new System.Drawing.Pen(Color.Black, 2), new Point(LeftPos, NodeOver.Bounds.Bottom), new Point(RightPos, NodeOver.Bounds.Bottom));
        }

        private void DrawFolderTopPlaceholders(TreeNode NodeOver)
        {
            Graphics g = this.CreateGraphics();
            int NodeOverImageWidth = this.ImageList.Images[NodeOver.ImageIndex].Size.Width + 8;

            int LeftPos, RightPos;
            LeftPos = NodeOver.Bounds.Left - NodeOverImageWidth;
            RightPos = this.Width - 4;

            Point[] LeftTriangle = new Point[5]{
                                                   new Point(LeftPos, NodeOver.Bounds.Top - 4),
                                                   new Point(LeftPos, NodeOver.Bounds.Top + 4),
                                                   new Point(LeftPos + 4, NodeOver.Bounds.Y),
                                                   new Point(LeftPos + 4, NodeOver.Bounds.Top - 1),
                                                   new Point(LeftPos, NodeOver.Bounds.Top - 5)};

            Point[] RightTriangle = new Point[5]{
                                                    new Point(RightPos, NodeOver.Bounds.Top - 4),
                                                    new Point(RightPos, NodeOver.Bounds.Top + 4),
                                                    new Point(RightPos - 4, NodeOver.Bounds.Y),
                                                    new Point(RightPos - 4, NodeOver.Bounds.Top - 1),
                                                    new Point(RightPos, NodeOver.Bounds.Top - 5)};


            g.FillPolygon(System.Drawing.Brushes.Black, LeftTriangle);
            g.FillPolygon(System.Drawing.Brushes.Black, RightTriangle);
            g.DrawLine(new System.Drawing.Pen(Color.Black, 2), new Point(LeftPos, NodeOver.Bounds.Top), new Point(RightPos, NodeOver.Bounds.Top));

        }
        private void DrawAddToFolderPlaceholder(TreeNode NodeOver)
        {
            Graphics g = this.CreateGraphics();
            int RightPos = NodeOver.Bounds.Right + 6;
            Point[] RightTriangle = new Point[5]{
                                                    new Point(RightPos, NodeOver.Bounds.Y + (NodeOver.Bounds.Height / 2) + 4),
                                                    new Point(RightPos, NodeOver.Bounds.Y + (NodeOver.Bounds.Height / 2) + 4),
                                                    new Point(RightPos - 4, NodeOver.Bounds.Y + (NodeOver.Bounds.Height / 2)),
                                                    new Point(RightPos - 4, NodeOver.Bounds.Y + (NodeOver.Bounds.Height / 2) - 1),
                                                    new Point(RightPos, NodeOver.Bounds.Y + (NodeOver.Bounds.Height / 2) - 5)};

            this.Refresh();
            g.FillPolygon(System.Drawing.Brushes.Black, RightTriangle);
        }

        private void SetNewNodeMap(TreeNode tnNode, bool boolBelowNode)
        {
            NewNodeMap.Length = 0;

            if (boolBelowNode)
                NewNodeMap.Insert(0, (int)tnNode.Index + 1);
            else
                NewNodeMap.Insert(0, (int)tnNode.Index);
            TreeNode tnCurNode = tnNode;

            while (tnCurNode.Parent != null)
            {
                tnCurNode = tnCurNode.Parent;

                if (NewNodeMap.Length == 0 && boolBelowNode == true)
                {
                    NewNodeMap.Insert(0, (tnCurNode.Index + 1) + "|");
                }
                else
                {
                    NewNodeMap.Insert(0, tnCurNode.Index + "|");
                }
            }
        }

        private bool SetMapsEqual()
        {
            if (this.NewNodeMap.ToString() == this.NodeMap)
                return true;
            else
            {
                this.NodeMap = this.NewNodeMap.ToString();
                return false;
            }
        }

        private Dictionary<string, bool> nodeStates;
        private string previousSelected;
        private TreeNode previousTop;

        public void SaveTreeState(TreeNode tn, string hold)
        {
            previousTop = TopNode;
            nodeStates = new Dictionary<string, bool>();
            SaveTreeStateRecursive(tn, nodeStates);
            previousSelected = hold;
        }
        private void SaveTreeStateRecursive(TreeNode tn, Dictionary<string, bool> nodeStates)
        {
            foreach (TreeNode childNode in tn.Nodes)
            {
                nodeStates.Add(childNode.Name, childNode.IsExpanded);
                // recursive
                SaveTreeStateRecursive(childNode, nodeStates);
            }
        }
        public void RestoreTreeState(TreeNode tn)
        {
            RestoreTreeStateRecursive(tn, nodeStates);
            // this node may have been deleted
            foreach (TreeNode tnHold in this.Nodes.Find(previousSelected, true))
            {
                this.SelectedNode = tnHold;
                break;
            }
            TopNode = previousTop;
        }

        private void RestoreTreeStateRecursive(TreeNode tn, Dictionary<string, bool> nodeStates)
        {
            foreach (TreeNode childNode in tn.Nodes)
            {
                if (nodeStates.ContainsKey(childNode.Name))
                {
                    if (nodeStates[childNode.Name])
                    {
                        childNode.Expand();
                    }
                    // for completeness.... ours will always be colapsed
                    else
                    {
                        childNode.Collapse();
                    }
                }
                // recursive
                RestoreTreeStateRecursive(childNode, nodeStates);
            }
        }
    }
}
