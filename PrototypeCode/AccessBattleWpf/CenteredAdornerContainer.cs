﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AccessBattleWpf
{
    public class CenteredAdornerContainer : Adorner
    {
        private Control _child;

        public CenteredAdornerContainer(UIElement adornedElement)
            : base(adornedElement) 
        {
        }

        double _verticalRatio = .5;
        public double VerticalRatio
        {
            get { return _verticalRatio; }
            set
            {
                _verticalRatio = value;
                if (_verticalRatio < 0) _verticalRatio = 0;
                if (_verticalRatio > 1) _verticalRatio = 1;
                InvalidateMeasure();
            }
        }

        protected override int VisualChildrenCount
        {
            get
            {
                return 1;
            }
        }

        public Control Child
        {
            get { return _child; }
            set
            {
                if (_child != null)
                {
                    RemoveVisualChild(_child);
                }
                _child = value;
                if (_child != null)
                {
                    AddVisualChild(_child);
                }
            }
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException();
            return _child;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            _child.Measure(constraint);
            return constraint;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var p = new Point(
                (finalSize.Width - _child.DesiredSize.Width) / 2,
                (finalSize.Height - _child.DesiredSize.Height) *_verticalRatio);            

            _child.Arrange(new Rect(p, _child.DesiredSize));
            return finalSize;
        }

    }
}