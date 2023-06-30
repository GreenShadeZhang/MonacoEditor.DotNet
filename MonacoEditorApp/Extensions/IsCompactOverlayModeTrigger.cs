﻿#nullable enable

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Windows.UI.ViewManagement;

namespace DevToys.UI.Extensions
{
    public sealed class IsCompactOverlayModeTrigger : StateTriggerBase
    {
        private FrameworkElement? _targetElement;

        public FrameworkElement? TargetElement
        {
            get
            {
                return _targetElement;
            }
            set
            {
                if (_targetElement is not null)
                {
                    _targetElement.SizeChanged -= OnSizeChanged;
                }
                _targetElement = value;
                if (_targetElement is not null)
                {
                    _targetElement.SizeChanged += OnSizeChanged;
                }
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var view = ApplicationView.GetForCurrentView();
            SetActive(view.ViewMode == ApplicationViewMode.CompactOverlay);
        }
    }
}
