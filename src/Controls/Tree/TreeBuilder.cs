#region Copyright ?2007 Rotem Sapir

/*
 * This software is provided 'as-is', without any express or implied warranty.
 * In no event will the authors be held liable for any damages arising from the
 * use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not claim
 * that you wrote the original software. If you use this software in a product,
 * an acknowledgment in the product documentation is required, as shown here:
 *
 * Portions Copyright ?2007 Rotem Sapir
 *
 * 2. No substantial portion of the source code of this library may be redistributed
 * without the express written permission of the copyright holders, where
 * "substantial" is defined as enough code to be recognizably from this library.
*/

#endregion Copyright ?2007 Rotem Sapir

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Xml;

namespace Xnlab.SQLMon.Controls.Tree
{
    public class TreeBuilder : IDisposable {

        #region Private Members

        private int _imgWidth;
        private int _imgHeight;
        private Graphics _gr;
        private Tree _tree;
        private double _percentageChangeX; // = ActualWidth / imgWidth;
        private double _percentageChangeY; // = ActualHeight / imgHeight;

        #endregion Private Members

        #region Public Properties

        public XmlDocument XmlTree { get; private set; }

        public Color BoxFillColor { get; set; } = Color.White;

        public int BoxWidth { get; set; } = 120;

        public int BoxHeight { get; set; } = 60;

        public int Margin { get; set; } = 20;

        public int HorizontalSpace { get; set; } = 30;

        public int VerticalSpace { get; set; } = 30;

        public int FontSize { get; set; } = 9;

        public Color LineColor { get; set; } = Color.Black;

        public float LineWidth { get; set; } = 2;

        public Color BgColor { get; set; } = Color.White;

        public Color FontColor { get; set; } = Color.Black;

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        ///     ctor
        /// </summary>
        /// <param name="TreeData"></param>
        public TreeBuilder(Tree data) {
            _tree = data;
        }

        public void Dispose() {
            _tree = null;

            if (_gr != null) {
                _gr.Dispose();
                _gr = null;
            }
        }

        /// <summary>
        ///     This overloaded method can be used to return the image using it's default calculated size, without resizing
        /// </summary>
        /// <param name="startFromNodeId"></param>
        /// <param name="imageType"></param>
        /// <returns></returns>
        public Stream GenerateTree(
            string startFromNodeId,
            ImageFormat imageType) {
            return GenerateTree(-1, -1, startFromNodeId, imageType);
        }

        /// <summary>
        ///     Creates the tree
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="startFromNodeId"></param>
        /// <param name="imageType"></param>
        /// <returns></returns>
        public Stream GenerateTree(int width,
            int height,
            string startFromNodeId,
            ImageFormat imageType) {
            var result = new MemoryStream();

            //reset image size
            _imgHeight = 0;
            _imgWidth = 0;
            //reset percentage change
            _percentageChangeX = 1.0;
            _percentageChangeY = 1.0;
            //define the image
            XmlTree = null;
            XmlTree = new XmlDocument();
            var rootDescription = string.Empty;
            var rootNote = string.Empty;
            var backColor = BoxFillColor;
            var foreColor = FontColor;
            var node = _tree.Find(startFromNodeId);
            if (node != null) {
                rootDescription = node.Description;
                rootNote = node.Note;
                backColor = node.BackColor;
                foreColor = node.ForeColor;
            }

            var rootNode = GetXmlNode(startFromNodeId, rootDescription, rootNote, backColor, foreColor);
            XmlTree.AppendChild(rootNode);
            BuildTree(rootNode, 0);

            //check for intersection. line below should be remarked if not debugging
            //as it affects performance measurably.
            //OverlapExists();
            var bmp = new Bitmap(_imgWidth, _imgHeight);
            _gr = Graphics.FromImage(bmp);
            _gr.Clear(BgColor);
            DrawChart(rootNode);

            //if caller does not care about size, use original calculated size
            if (width < 0) width = _imgWidth;
            if (height < 0) height = _imgHeight;

            var resizedBmp = new Bitmap(bmp, new Size(width, height));
            //after resize, determine the change percentage
            _percentageChangeX = Convert.ToDouble(width) / _imgWidth;
            _percentageChangeY = Convert.ToDouble(height) / _imgHeight;
            //after resize - change the coordinates of the list, in order return the proper coordinates
            //for each node
            if (_percentageChangeX != 1.0 || _percentageChangeY != 1.0) CalculateImageMapData();
            resizedBmp.Save(result, imageType);
            resizedBmp.Dispose();
            bmp.Dispose();
            _gr.Dispose();
            return result;
        }

        /// <summary>
        ///     the node holds the x,y in attributes
        ///     use them to calculate the position
        ///     This is public so it can be used by other classes trying to calculate the
        ///     cursor/mouse location
        /// </summary>
        /// <param name="oNode"></param>
        /// <returns></returns>
        public Rectangle GetRectangleFromNode(XmlNode oNode) {
            if (oNode.Attributes["X"] == null || oNode.Attributes["Y"] == null)
                throw new Exception("Both attributes X,Y must exist for node.");
            var x = Convert.ToInt32(oNode.Attributes["X"].InnerText);
            var y = Convert.ToInt32(oNode.Attributes["Y"].InnerText);

            var result = new Rectangle(x, y, (int) (BoxWidth * _percentageChangeX),
                (int) (BoxHeight * _percentageChangeY));
            return result;
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        ///     convert the datatable to an XML document
        /// </summary>
        /// <param name="oNode"></param>
        /// <param name="y"></param>
        private void BuildTree(XmlNode oNode, int y) {
            XmlNode childNode = null;
            //has children
            foreach (var childRow in _tree.Parents(oNode.Attributes["nodeID"].InnerText)) {
                //for each child node call this function again
                childNode = GetXmlNode(childRow.Id, childRow.Description, childRow.Note, childRow.BackColor,
                    childRow.ForeColor);
                oNode.AppendChild(childNode);
                BuildTree(childNode, y + 1);
            }

            //build node data
            //after checking for nodes we can add the current node
            int startX;
            int startY;
            var resultsArr = new[] {
                GetXPosByOwnChildren(oNode),
                GetXPosByParentPreviousSibling(oNode),
                GetXPosByPreviousSibling(oNode),
                Margin
            };
            Array.Sort(resultsArr);
            startX = resultsArr[3];
            startY = y * (BoxHeight + VerticalSpace) + Margin;
            var width = BoxWidth;
            var height = BoxHeight;
            //update the coordinates of this box into the matrix, for later calculations
            oNode.Attributes["X"].InnerText = startX.ToString();
            oNode.Attributes["Y"].InnerText = startY.ToString();

            //update the image size
            if (_imgWidth < startX + width + Margin) _imgWidth = startX + width + Margin;
            if (_imgHeight < startY + height + Margin) _imgHeight = startY + height + Margin;
        }

        /************************************************************************************************************************
         * The box position is affected by:
         * 1. The previous sibling (box on the same level)
         * 2. The positions of it's children
         * 3. The position of it's uncle (parents' previous sibling)/ cousins (parents' previous sibling children)
         * What determines the position is the farthest x of all the above. If all/some of the above have no value, the margin
         * becomes the dtermining factor.
         * **********************************************************************************************************************
        */

        private int GetXPosByPreviousSibling(XmlNode currentNode) {
            var result = -1;
            var x = -1;
            var prevSibling = currentNode.PreviousSibling;
            if (prevSibling != null) {
                if (prevSibling.HasChildNodes) {
                    //Result = Convert.ToInt32(PrevSibling.LastChild.Attributes["X"].InnerText ) + _BoxWidth + _HorizontalSpace;
                    //need to loop through all children for all generations of previous sibling
                    x = Convert.ToInt32(GetMaxXOfDescendants(prevSibling.LastChild));
                    result = x + BoxWidth + HorizontalSpace;
                }
                else {
                    result = Convert.ToInt32(prevSibling.Attributes["X"].InnerText) + BoxWidth + HorizontalSpace;
                }
            }

            return result;
        }

        private int GetXPosByOwnChildren(XmlNode currentNode) {
            var result = -1;

            if (currentNode.HasChildNodes) {
                var lastChildX = Convert.ToInt32(currentNode.LastChild.Attributes["X"].InnerText);
                var firstChildX = Convert.ToInt32(currentNode.FirstChild.Attributes["X"].InnerText);
                result = (lastChildX + BoxWidth - firstChildX) / 2 - BoxWidth / 2 + firstChildX;
            }

            return result;
        }

        private int GetXPosByParentPreviousSibling(XmlNode currentNode) {
            var result = -1;
            var x = -1;
            var parentPrevSibling = currentNode.ParentNode.PreviousSibling;

            if (parentPrevSibling != null) {
                if (parentPrevSibling.HasChildNodes) {
                    //X = Convert.ToInt32(ParentPrevSibling.LastChild.Attributes["X"].InnerText);
                    x = GetMaxXOfDescendants(parentPrevSibling.LastChild);
                    result = x + BoxWidth + HorizontalSpace;
                }
                else {
                    x = Convert.ToInt32(parentPrevSibling.Attributes["X"].InnerText);
                    result = x + BoxWidth + HorizontalSpace;
                }
            }
            else //ParentPrevSibling == null
            {
                if (currentNode.ParentNode.Name != "#document")
                    result = GetXPosByParentPreviousSibling(currentNode.ParentNode);
            }

            return result;
        }

        /// <summary>
        ///     Get the maximum x of the lowest child on the current tree of nodes
        ///     Recursion does not work here, so we'll use a loop to climb down the tree
        ///     Recursion is not a solution because we need to return the value of the last leaf of the tree.
        ///     That would require managing a global variable.
        /// </summary>
        /// <param name="currentNode"></param>
        /// <returns></returns>
        private int GetMaxXOfDescendants(XmlNode currentNode) {
            var result = -1;

            while (currentNode.HasChildNodes) currentNode = currentNode.LastChild;

            result = Convert.ToInt32(currentNode.Attributes["X"].InnerText);

            return result;
            //int Result = -1;
            //if (CurrentNode.HasChildNodes)
            //{
            //    GetMaxXOfDescendants(CurrentNode.LastChild);
            //}
            //else
            //{
            //    Result = Convert.ToInt32(CurrentNode.Attributes["X"].InnerText);
            //}
            //return Result;
        }

        /// <summary>
        ///     create an xml node based on supplied data
        /// </summary>
        /// <returns></returns>
        private XmlNode GetXmlNode(string nodeId, string nodeDescription, string nodeNote, Color backColor,
            Color foreColor) {
            //build the node
            XmlNode resultNode = XmlTree.CreateElement("Node");
            var attNodeId = XmlTree.CreateAttribute("nodeID");

            var attNodeDescription = XmlTree.CreateAttribute("nodeDescription");
            var attNodeNote = XmlTree.CreateAttribute("nodeNote");
            var attStartX = XmlTree.CreateAttribute("X");
            var attStartY = XmlTree.CreateAttribute("Y");
            var attBackColor = XmlTree.CreateAttribute("nodeBackColor");
            var attForeColor = XmlTree.CreateAttribute("nodeForeColor");

            //set the values of what we know
            attNodeId.InnerText = nodeId;

            attNodeDescription.InnerText = nodeDescription;
            attNodeNote.InnerText = nodeNote;
            attStartX.InnerText = "0";
            attStartY.InnerText = "0";
            attBackColor.InnerText = backColor.ToArgb().ToString();
            attForeColor.InnerText = foreColor.ToArgb().ToString();

            resultNode.Attributes.Append(attNodeId);

            resultNode.Attributes.Append(attNodeDescription);
            resultNode.Attributes.Append(attNodeNote);
            resultNode.Attributes.Append(attStartX);
            resultNode.Attributes.Append(attStartY);
            resultNode.Attributes.Append(attBackColor);
            resultNode.Attributes.Append(attForeColor);

            return resultNode;
        }

        /// <summary>
        ///     Draws the actual chart image.
        /// </summary>
        private void DrawChart(XmlNode oNode) {
            // Create font and brush.
            var drawFont = new Font("verdana", FontSize);
            var drawBrush = new SolidBrush(Color.FromArgb(Convert.ToInt32(oNode.Attributes["nodeForeColor"].Value)));
            var boxPen = new Pen(LineColor, LineWidth);
            var drawFormat = new StringFormat();
            drawFormat.Alignment = StringAlignment.Center;
            //find children

            foreach (XmlNode childNode in oNode.ChildNodes) DrawChart(childNode);
            var currentRectangle = GetRectangleFromNode(oNode);
            _gr.DrawRectangle(boxPen, currentRectangle);
            _gr.FillRectangle(new SolidBrush(Color.FromArgb(Convert.ToInt32(oNode.Attributes["nodeBackColor"].Value))),
                currentRectangle);
            // Create string to draw.
            var drawString = Environment.NewLine + oNode.Attributes["nodeNote"].InnerText; // +
            //Environment.NewLine +
            // oNode.Attributes["nodeDescription"].InnerText;

            // Draw string to screen.
            _gr.DrawString(drawString, drawFont, drawBrush, currentRectangle, drawFormat);

            //draw connecting lines

            if (oNode.ParentNode.Name != "#document")
                _gr.DrawLine(boxPen, currentRectangle.Left + BoxWidth / 2,
                    currentRectangle.Top,
                    currentRectangle.Left + BoxWidth / 2,
                    currentRectangle.Top - VerticalSpace / 2);
            if (oNode.HasChildNodes)
                _gr.DrawLine(boxPen, currentRectangle.Left + BoxWidth / 2,
                    currentRectangle.Top + BoxHeight,
                    currentRectangle.Left + BoxWidth / 2,
                    currentRectangle.Top + BoxHeight + VerticalSpace / 2);
            if (oNode.PreviousSibling != null)
                _gr.DrawLine(boxPen, GetRectangleFromNode(oNode.PreviousSibling).Left + BoxWidth / 2 - LineWidth / 2,
                    GetRectangleFromNode(oNode.PreviousSibling).Top - VerticalSpace / 2,
                    currentRectangle.Left + BoxWidth / 2 + LineWidth / 2,
                    currentRectangle.Top - VerticalSpace / 2);
        }

        /// <summary>
        ///     After resizing the image, all positions of the rectanlges need to be
        ///     recalculated too.
        /// </summary>
        /// <param name="ActualWidth"></param>
        /// <param name="ActualHeight"></param>
        private void CalculateImageMapData() {
            var x = 0;
            var newX = 0;
            var y = 0;
            var newY = 0;
            foreach (XmlNode oNode in XmlTree.SelectNodes("//Node")) {
                //go through all nodes and resize the coordinates
                x = Convert.ToInt32(oNode.Attributes["X"].InnerText);
                y = Convert.ToInt32(oNode.Attributes["Y"].InnerText);
                newX = (int) (x * _percentageChangeX);
                newY = (int) (y * _percentageChangeY);
                oNode.Attributes["X"].InnerText = newX.ToString();
                oNode.Attributes["Y"].InnerText = newY.ToString();
            }
        }

        /// <summary>
        ///     used for testing purposes, to see if overlap exists between at least 2 boxes.
        /// </summary>
        /// <returns></returns>
        private bool OverlapExists() {
            var listOfRectangles = new List<Rectangle>(); //the list of all objects
            int x;
            int y;
            Rectangle currentRect;
            foreach (XmlNode oNode in XmlTree.SelectNodes("//Node")) {
                //go through all nodes and resize the coordinates
                x = Convert.ToInt32(oNode.Attributes["X"].InnerText);
                y = Convert.ToInt32(oNode.Attributes["Y"].InnerText);
                currentRect = new Rectangle(x, y, BoxWidth, BoxHeight);
                //before adding the node we check if the space it is supposed to occupy is already occupied.
                foreach (var rect in listOfRectangles)
                    if (currentRect.IntersectsWith(rect))
                        return true;
                listOfRectangles.Add(currentRect);
            }

            return false;
        }

        #endregion Private Methods
    }
}