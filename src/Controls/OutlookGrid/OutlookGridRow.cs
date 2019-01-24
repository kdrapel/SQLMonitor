// Copyright 2006 Herre Kuijpers - <herre@xs4all.nl>
//
// This source file(s) may be redistributed, altered and customized
// by any means PROVIDING the authors name and all copyright
// notices remain intact.
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED. USE IT AT YOUR OWN RISK. THE AUTHOR ACCEPTS NO
// LIABILITY FOR ANY DATA DAMAGE/LOSS THAT THIS PRODUCT MAY CAUSE.
//-----------------------------------------------------------------------

using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Xnlab.SQLMon.Controls.OutlookGrid
{
    #region OutlookGridRow - subclasses the DataGridView's DataGridViewRow class

    /// <summary>
    ///     In order to support grouping with the same look & feel as Outlook, the behaviour
    ///     of the DataGridViewRow is overridden by the OutlookGridRow.
    ///     The OutlookGridRow has 2 main additional properties: the Group it belongs to and
    ///     a the IsRowGroup flag that indicates whether the OutlookGridRow object behaves like
    ///     a regular row (with data) or should behave like a Group row.
    /// </summary>
    public class OutlookGridRow : DataGridViewRow {

        public OutlookGridRow() : this(null, false) {
        }

        public OutlookGridRow(IOutlookGridGroup group)
            : this(group, false) {
        }

        public OutlookGridRow(IOutlookGridGroup group, bool isGroupRow) {
            Group = group;
            IsGroupRow = isGroupRow;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IOutlookGridGroup Group { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsGroupRow { get; set; }

        public override DataGridViewElementStates GetState(int rowIndex) {
            if (!IsGroupRow && Group != null && Group.Collapsed)
                return base.GetState(rowIndex) & DataGridViewElementStates.Selected;

            return base.GetState(rowIndex);
        }

        /// <summary>
        ///     the main difference with a Group row and a regular row is the way it is painted on the control.
        ///     the Paint method is therefore overridden and specifies how the Group row is painted.
        ///     Note: this method is not implemented optimally. It is merely used for demonstration purposes
        /// </summary>
        /// <param name="graphics"></param>
        /// <param name="clipBounds"></param>
        /// <param name="rowBounds"></param>
        /// <param name="rowIndex"></param>
        /// <param name="rowState"></param>
        /// <param name="isFirstDisplayedRow"></param>
        /// <param name="isLastVisibleRow"></param>
        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle rowBounds, int rowIndex,
            DataGridViewElementStates rowState, bool isFirstDisplayedRow, bool isLastVisibleRow) {
            if (IsGroupRow) {
                var grid = (OutlookGrid) DataGridView;
                var rowHeadersWidth = grid.RowHeadersVisible ? grid.RowHeadersWidth : 0;

                // this can be optimized
                Brush brush = new SolidBrush(grid.DefaultCellStyle.BackColor);
                Brush brush2 = new SolidBrush(Color.FromKnownColor(KnownColor.GradientActiveCaption));

                var gridwidth = grid.Columns.GetColumnsWidth(DataGridViewElementStates.Displayed);
                var rowBounds2 = grid.GetRowDisplayRectangle(Index, true);

                // draw the background
                graphics.FillRectangle(brush, rowBounds.Left + rowHeadersWidth - grid.HorizontalScrollingOffset,
                    rowBounds.Top, gridwidth, rowBounds.Height - 1);

                // draw text, using the current grid font
                graphics.DrawString(Group.Text, grid.Font, Brushes.Black,
                    rowHeadersWidth - grid.HorizontalScrollingOffset + 23, rowBounds.Bottom - 18);

                //draw bottom line
                graphics.FillRectangle(brush2, rowBounds.Left + rowHeadersWidth - grid.HorizontalScrollingOffset,
                    rowBounds.Bottom - 2, gridwidth - 1, 2);

                // draw right vertical bar
                if (grid.CellBorderStyle == DataGridViewCellBorderStyle.SingleVertical ||
                    grid.CellBorderStyle == DataGridViewCellBorderStyle.Single)
                    graphics.FillRectangle(brush2,
                        rowBounds.Left + rowHeadersWidth - grid.HorizontalScrollingOffset + gridwidth - 1,
                        rowBounds.Top, 1, rowBounds.Height);

                if (Group.Collapsed) {
                    if (grid.ExpandIcon != null)
                        graphics.DrawImage(grid.ExpandIcon,
                            rowBounds.Left + rowHeadersWidth - grid.HorizontalScrollingOffset + 4,
                            rowBounds.Bottom - 18, 11, 11);
                }
                else {
                    if (grid.CollapseIcon != null)
                        graphics.DrawImage(grid.CollapseIcon,
                            rowBounds.Left + rowHeadersWidth - grid.HorizontalScrollingOffset + 4,
                            rowBounds.Bottom - 18, 11, 11);
                }

                brush.Dispose();
                brush2.Dispose();
            }

            base.Paint(graphics, clipBounds, rowBounds, rowIndex, rowState, isFirstDisplayedRow, isLastVisibleRow);
        }

        protected override void PaintCells(Graphics graphics, Rectangle clipBounds, Rectangle rowBounds, int rowIndex,
            DataGridViewElementStates rowState, bool isFirstDisplayedRow, bool isLastVisibleRow,
            DataGridViewPaintParts paintParts) {
            if (!IsGroupRow)
                base.PaintCells(graphics, clipBounds, rowBounds, rowIndex, rowState, isFirstDisplayedRow,
                    isLastVisibleRow, paintParts);
        }

        /// <summary>
        ///     this function checks if the user hit the expand (+) or collapse (-) icon.
        ///     if it was hit it will return true
        /// </summary>
        /// <param name="e">mouse click event arguments</param>
        /// <returns>returns true if the icon was hit, false otherwise</returns>
        internal bool IsIconHit(DataGridViewCellMouseEventArgs e) {
            if (e.ColumnIndex < 0) return false;

            var grid = (OutlookGrid) DataGridView;
            var rowBounds = grid.GetRowDisplayRectangle(Index, false);
            var x = e.X;

            var c = grid.Columns[e.ColumnIndex];
            if (IsGroupRow &&
                c.DisplayIndex == 0 &&
                x > rowBounds.Left + 4 &&
                x < rowBounds.Left + 16 &&
                e.Y > rowBounds.Height - 18 &&
                e.Y < rowBounds.Height - 7)
                return true;

            return false;

            //System.Diagnostics.Debug.WriteLine(e.ColumnIndex);
        }
    }

    #endregion OutlookGridRow - subclasses the DataGridView's DataGridViewRow class

    #region OutlookGridRowComparer implementation

    /// <summary>
    ///     the OutlookGridRowComparer object is used to sort unbound data in the OutlookGrid.
    ///     currently the comparison is only done for string values.
    ///     therefore dates or numbers may not be sorted correctly.
    ///     Note: this class is not implemented optimally. It is merely used for demonstration purposes
    /// </summary>
    internal class OutlookGridRowComparer : IComparer {
        private readonly int _columnIndex;
        private readonly ListSortDirection _direction;

        public OutlookGridRowComparer(int columnIndex, ListSortDirection direction) {
            _columnIndex = columnIndex;
            _direction = direction;
        }

        #region IComparer Members

        public int Compare(object x, object y) {
            var obj1 = (OutlookGridRow) x;
            var obj2 = (OutlookGridRow) y;
            return string.Compare(obj1.Cells[_columnIndex].Value.ToString(),
                       obj2.Cells[_columnIndex].Value.ToString()) *
                   (_direction == ListSortDirection.Ascending ? 1 : -1);
        }

        #endregion IComparer Members
    }

    #endregion OutlookGridRowComparer implementation
}