﻿/****************************** Module Header ******************************\
* Module Name:  Page.cs
* Project:      CSSL3DeepZoom
* Copyright (c) Microsoft Corporation.
* 
* This example demonstrates how to work with deep zoom programmatically in Silverlight using C#.
* 
* This source is subject to the Microsoft Public License.
* See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL.
* All other rights reserved.
* 
* History:
* * 8/27/2009 15:40 Yilun Luo Created
\***************************************************************************/

using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Runtime.Serialization;
using System.Xml.Linq;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using CSSL3DeepZoom.DeepZoomServiceReference;
using System.ServiceModel;

namespace CSSL3DeepZoom
{
	public partial class Page : UserControl
	{
		//
		// Based on prior work done by Lutz Gerhard, Peter Blois, and Scott Hanselman
		//

		private Dictionary<int, ImageMetadata> _imageMetadatas;
		private Dictionary<int, ConversationControl> _conversations;
		private Point? _previousPositon;
		private Point _currentPosition;
		private Point[] _originalSubImageViewportOrigions;
		private bool _msiLoaded;

		//The following fields are generated by Deep Zoom Composer.
		private double _zoom = 1;
		private bool _duringDrag = false;
		private bool _mouseDown = false;
		private Point _lastMouseDownPos = new Point();
		private Point _lastMousePos = new Point();
		private Point _lastMouseViewPort = new Point();


		public double ZoomFactor
		{
			get { return _zoom; }
			set { _zoom = value; }
		}

		public Page()
		{
			InitializeComponent();

			//Call a WCF service to download source images and generate deep zoom content.
			//Begin Comment:
			//Comment the following lines if you want to provide your own content.
			string serviceUri = App.Current.Host.Source.ToString();
			serviceUri = serviceUri.Substring(0, serviceUri.LastIndexOf("/ClientBin")) + "/GenerateDeepZoomService.svc";
			EndpointAddress address = new EndpointAddress(serviceUri);
			BasicHttpBinding binding = new BasicHttpBinding();
			GenerateDeepZoomServiceClient client = new GenerateDeepZoomServiceClient(binding, address);
			client.PrepareDeepZoomCompleted += new EventHandler<PrepareDeepZoomCompletedEventArgs>(client_PrepareDeepZoomCompleted);
			//Pass true if you want to force programatically generating deep zoom content. Pass false if you want to avoid generating the content again when it already exists.
			client.PrepareDeepZoomAsync(false);
			//End Comment.

			//Download Metadata.xml.
			//Begin download metadata.
			WebClient webClient = new WebClient();
			webClient.DownloadStringCompleted += new DownloadStringCompletedEventHandler(webClient_DownloadStringCompleted);
			webClient.DownloadStringAsync(new Uri("GeneratedImages/Metadata.xml", UriKind.Relative));
			//End download metadata.

			this.Loaded += new RoutedEventHandler(Page_Loaded);

			//
			// Firing an event when the MultiScaleImage is Loaded
			//
			this.msi.Loaded += new RoutedEventHandler(msi_Loaded);

			//
			// Firing an event when all of the images have been Loaded
			//
			this.msi.ImageOpenSucceeded += new RoutedEventHandler(msi_ImageOpenSucceeded);

			//
			// Handling all of the mouse and keyboard functionality
			//
			this.MouseMove += delegate(object sender, MouseEventArgs e)
			{
				_lastMousePos = e.GetPosition(msi);

				if (_duringDrag)
				{
					Point newPoint = _lastMouseViewPort;
					newPoint.X += (_lastMouseDownPos.X - _lastMousePos.X) / msi.ActualWidth * msi.ViewportWidth;
					newPoint.Y += (_lastMouseDownPos.Y - _lastMousePos.Y) / msi.ActualWidth * msi.ViewportWidth;
					msi.ViewportOrigin = newPoint;
				}
			};

			this.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
			{
				if (this._msiLoaded)
				{
					_lastMouseDownPos = e.GetPosition(msi);
					_lastMouseViewPort = msi.ViewportOrigin;

					_mouseDown = true;
					msi.CaptureMouse();
				}
			};

			this.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
			{
				if (!this._msiLoaded)
				{
					return;
				}
				_mouseDown = false;

				msi.ReleaseMouseCapture();

				//Move the ball to the new position.
				if (this.moveBallToggleButton.IsChecked.Value && !_duringDrag)
				{
					this.MoveBallAndSubImages(e);
				}
				else if (!_duringDrag)
				{
					bool shiftDown = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
					double newzoom = _zoom;

					if (shiftDown)
					{
						newzoom /= 2;
					}
					else
					{
						newzoom *= 2;
					}

					Zoom(newzoom, msi.ElementToLogicalPoint(this._lastMousePos));
				}
				_duringDrag = false;

			};

			this.MouseMove += delegate(object sender, MouseEventArgs e)
			{
				_lastMousePos = e.GetPosition(msi);
				if (_mouseDown && !_duringDrag)
				{
					_duringDrag = true;
					double w = msi.ViewportWidth;
					Point o = new Point(msi.ViewportOrigin.X, msi.ViewportOrigin.Y);
					//msi.UseSprings = false;
					msi.ViewportOrigin = new Point(o.X, o.Y);
					msi.ViewportWidth = w;
					_zoom = 1 / w;
					//msi.UseSprings = true;
				}
				else if (_mouseDown)
				{
					this._currentPosition = e.GetPosition(this.msi);
					Point newPoint = _lastMouseViewPort;
				}

				//Update the ball's matrix's OffsetX/Y when dragging.
				if (this._duringDrag)
				{
					this.UpdateTranslation();
				}

				this._previousPositon = e.GetPosition(this.msi);

				//Displays the tag for a sub image that hit tested.
				this.HitTestImage();

				if (_duringDrag)
				{
					Point newPoint = _lastMouseViewPort;
					newPoint.X += (_lastMouseDownPos.X - _lastMousePos.X) / msi.ActualWidth * msi.ViewportWidth;
					newPoint.Y += (_lastMouseDownPos.Y - _lastMousePos.Y) / msi.ActualWidth * msi.ViewportWidth;
					msi.ViewportOrigin = newPoint;
				}
			};

			new MouseWheelHelper(this).Moved += delegate(object sender, MouseWheelEventArgs e)
			{
				e.Handled = true;

				double newzoom = _zoom;

				if (e.Delta < 0)
					newzoom /= 1.3;
				else
					newzoom *= 1.3;

				Zoom(newzoom, msi.ElementToLogicalPoint(this._lastMousePos));
				msi.CaptureMouse();
			};
		}

		void Page_Loaded(object sender, RoutedEventArgs e)
		{
			string path = App.Current.Resources["path"].ToString();
			string zoomIn = App.Current.Resources["zoomIn"].ToString();

			_zoom = Int32.Parse(zoomIn);
			_zoom = 1;

			//Uncomment this line if you want to provide your own source, without generate the deep zoom content programatically.
			//this.DisplayDeepZoom();
		}

		/// <summary>
		/// Display the tag for a sub image that hit tested.
		/// </summary>
		private void HitTestImage()
		{
			for (int i = 0; i < this.msi.SubImages.Count; i++)
			{
				MultiScaleSubImage subImage = this.msi.SubImages[i];
				if (HitTest(subImage))
				{
					//ZOrder starts from 1 rather than 0.
					ImageMetadata metadata = this._imageMetadatas[i + 1];
					ConversationControl conversationControl = this._conversations[i + 1];
					Point topLeft = this.msi.LogicalToElementPoint(new Point(-subImage.ViewportOrigin.X / subImage.ViewportWidth, -subImage.ViewportOrigin.Y / subImage.ViewportWidth + 1 / subImage.ViewportWidth / subImage.AspectRatio));
					conversationControl.translate.X = topLeft.X;
					conversationControl.translate.Y = topLeft.Y;
					conversationControl.Visibility = Visibility.Visible;
				}
				else
				{
					ConversationControl conversationControl = this._conversations[i + 1];
					conversationControl.Visibility = Visibility.Collapsed;
				}
			}
		}

		/// <summary>
		/// Perform a hit test on the sub image.
		/// </summary>
		/// <param name="subimage">The sub image.</param>
		private bool HitTest(MultiScaleSubImage subImage)
		{
			//ViewportWitdh determines how large the sub image is.
			//Calculate the width and height of the sub image when its ViewportWidth is 1. That is, the original size. When you zoom out, ViewportWidth will increase, and when you zoom in, ViewportWidth will decrease.
			double width = this.msi.ActualWidth / (this.msi.ViewportWidth * subImage.ViewportWidth);
			//There's no ViewportHeight property. It is calculated by ViewportWidth * AspectRatio.
			double height = this.msi.ActualWidth / (this.msi.ViewportWidth * subImage.ViewportWidth * subImage.AspectRatio);
			//ViewportOrigin determines where the image is. Note the coordinate is always 0 or negative. Together with ViewportWidth, we can find the top left point of the sub image.
			Point topLeft = this.msi.LogicalToElementPoint(new Point(-subImage.ViewportOrigin.X / subImage.ViewportWidth, -subImage.ViewportOrigin.Y / subImage.ViewportWidth)
			);
			Rect rect = new Rect(topLeft.X, topLeft.Y, width, height);
			//Hit.
			if (rect.Contains(_lastMousePos))
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// Update the ball's matrix's OffsetX/Y when dragging.
		/// </summary>
		private void UpdateTranslation()
		{
			double m11 = this.ballTransform.Matrix.M11;
			double m22 = this.ballTransform.Matrix.M11;
			double offsetX = this.ballTransform.Matrix.OffsetX;
			double offsetY = this.ballTransform.Matrix.OffsetY;
			if (this._previousPositon != null)
			{
				offsetX += this._lastMousePos.X - this._previousPositon.Value.X;
				offsetY += this._lastMousePos.Y - this._previousPositon.Value.Y;
			}
			Matrix matrix = new Matrix(m11, this.ballTransform.Matrix.M12, this.ballTransform.Matrix.M21, m22, offsetX, offsetY);
			this.ballTransform.Matrix = matrix;
		}

		/// <summary>
		/// Move the ball to the clicked position. Move sub images as well.
		/// </summary>
		/// <param name="e"></param>
		private void MoveBallAndSubImages(MouseButtonEventArgs e)
		{
			Point delta = e.GetPosition(this.ball);
			//The ball may have been scaled. So the delta reported by e.GetPostion must take scaling into account.
			delta = new Point(delta.X * this.ballTransform.Matrix.M11, delta.Y * this.ballTransform.Matrix.M11);
			//Move the ball.
			Matrix matrix = new Matrix(this.ballTransform.Matrix.M11, 0, 0, this.ballTransform.Matrix.M22,
				this.ballTransform.Matrix.OffsetX + delta.X,
				this.ballTransform.Matrix.OffsetY + delta.Y);
			this.ballTransform.Matrix = matrix;

			//Use ElementToLogicalPoint to translate delta from the global coordinate to the MultiScaleImage's logical coordinate system.
			Point logicalDetla = this.msi.ElementToLogicalPoint(delta);
			//Move every sub image, except the background.
			for (int i = 1; i < this.msi.SubImages.Count; i++)
			{
				this.msi.SubImages[i].ViewportOrigin = new Point(
					this.msi.SubImages[i].ViewportOrigin.X - (logicalDetla.X - this.msi.ViewportOrigin.X) * this.msi.SubImages[i].ViewportWidth,
					this.msi.SubImages[i].ViewportOrigin.Y - (logicalDetla.Y - this.msi.ViewportOrigin.Y) * this.msi.SubImages[i].ViewportWidth);
			}
		}

		/// <summary>
		/// When Metadata.xml is downloaded, let's parse it and create a list of ImageMetadata objects. ZOrder can be used to uniquely identify an image. So let's use it as the key of our dictionary for faster searching.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void webClient_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
		{
			XDocument doc = XDocument.Parse(e.Result);
			this._imageMetadatas = new Dictionary<int, ImageMetadata>();
			this._conversations = new Dictionary<int, ConversationControl>();
			var imageElements = doc.Root.Elements("Image");
			foreach (XElement imageElement in imageElements)
			{
				int zOrder = int.Parse(imageElement.Element("ZOrder").Value);
				ImageMetadata metadata = new ImageMetadata()
				{
					FileName = imageElement.Element("FileName").Value,
					X = double.Parse(imageElement.Element("x").Value),
					Y = double.Parse(imageElement.Element("y").Value),
					Width = double.Parse(imageElement.Element("Width").Value),
					Height = double.Parse(imageElement.Element("Height").Value),
					ZOrder = zOrder,
					Tag = imageElement.Element("Tag").Value
				};
				this._imageMetadatas.Add(zOrder, metadata);
				ConversationControl conversationControl = new ConversationControl() { ConversationText = metadata.Tag, Visibility = Visibility.Collapsed };
				this.LayoutRoot.Children.Add(conversationControl);
				this._conversations.Add(zOrder, conversationControl);
			}
		}

		void msi_ImageOpenSucceeded(object sender, RoutedEventArgs e)
		{
			//Store the original ViewportOrigins, so we can "go home" very easily.
			int count = this.msi.SubImages.Count;
			this._originalSubImageViewportOrigions = new Point[count];
			for (int i = 0; i < count; i++)
			{
				this._originalSubImageViewportOrigions[i] = this.msi.SubImages[i].ViewportOrigin;
			}
			msi.ViewportWidth = 1;
		}

		void msi_Loaded(object sender, RoutedEventArgs e)
		{
		}
		
		/// <summary>
		/// Zoom the ball together with the MultiScaleImage.
		/// </summary>
		/// <param name="newzoom"></param>
		/// <param name="p"></param>
		private void Zoom(double newzoom, Point p)
		{
			if (newzoom < 0.5)
			{
				newzoom = 0.5;
			}

			MatrixTransform relativeTransform = (MatrixTransform)this.ball.TransformToVisual(this.msi);
			Point relativeDelta = this.msi.ElementToLogicalPoint(new Point(relativeTransform.Matrix.OffsetX, relativeTransform.Matrix.OffsetY));
			msi.ZoomAboutLogicalPoint(newzoom / _zoom, p.X, p.Y);
			this.ZoomBall(newzoom, relativeTransform);

			_zoom = newzoom;
		}

		/// <summary>
		/// Zoom the ball.
		/// </summary>
		/// <param name="newzoom"></param>
		/// <param name="relativeTransform"></param>
		private void ZoomBall(double newzoom, MatrixTransform relativeTransform)
		{
			//The mouse position minuse top left of the ball's bound.
			double deltaX = this._lastMousePos.X - relativeTransform.Matrix.OffsetX;
			double deltaY = this._lastMousePos.Y - relativeTransform.Matrix.OffsetY;
			//Pass the new zoom to M11 and M22.
			double m11 = newzoom;
			double m22 = newzoom;
			//The new offset is calculated by multiply the delta with (1 - this time's zoom value). Then let's add it to the previous offset. 
			double offsetX = this.ballTransform.Matrix.OffsetX + deltaX * (1 - newzoom / this.ballTransform.Matrix.M11);
			double offsetY = this.ballTransform.Matrix.OffsetY + deltaY * (1 - newzoom / this.ballTransform.Matrix.M22);
			Matrix matrix = new Matrix(m11, this.ballTransform.Matrix.M12, this.ballTransform.Matrix.M21, m22, offsetX, offsetY);
			this.ballTransform.Matrix = matrix;
		}

		private void GoHomeClick(object sender, System.Windows.RoutedEventArgs e)
		{
			this.msi.ViewportWidth = 1;
			this.msi.ViewportOrigin = new Point(0, 0);
			this.ballTransform.Matrix = new Matrix();
			int count = this.msi.SubImages.Count;
			for (int i = 0; i < count; i++)
			{
				this.msi.SubImages[i].ViewportOrigin = this._originalSubImageViewportOrigions[i];
			}
			ZoomFactor = 1;
		}

		// Handling the VSM states
		private void LeaveMovie(object sender, System.Windows.Input.MouseEventArgs e)
		{
			VisualStateManager.GoToState(this, "FadeOut", true);
		}

		private void EnterMovie(object sender, System.Windows.Input.MouseEventArgs e)
		{
			VisualStateManager.GoToState(this, "FadeIn", true);
		}


		// unused functions that show the inner math of Deep Zoom
		public Rect getImageRect()
		{
			return new Rect(-msi.ViewportOrigin.X / msi.ViewportWidth, -msi.ViewportOrigin.Y / msi.ViewportWidth, 1 / msi.ViewportWidth, 1 / msi.ViewportWidth * msi.AspectRatio);
		}

		public Rect ZoomAboutPoint(Rect img, double zAmount, Point pt)
		{
			return new Rect(pt.X + (img.X - pt.X) / zAmount, pt.Y + (img.Y - pt.Y) / zAmount, img.Width / zAmount, img.Height / zAmount);
		}

		public void LayoutDZI(Rect rect)
		{
			double ar = msi.AspectRatio;
			msi.ViewportWidth = 1 / rect.Width;
			msi.ViewportOrigin = new Point(-rect.Left / rect.Width, -rect.Top / rect.Width);
		}

		private void msi_ViewportChanged(object sender, RoutedEventArgs e)
		{
		}

		/// <summary>
		/// This method is a helper event handlers that makes your user experience better.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void client_PrepareDeepZoomCompleted(object sender, PrepareDeepZoomCompletedEventArgs e)
		{
			if (e.Result)
			{
				this.DisplayDeepZoom();
			}
			else
			{
				this.informationTextBlock.Text = @"Failed to generate the deep zoom content. Please check if you have write access to the DeepZoomWebSite\ClientBin\GeneratedImages directory. If you're using a local IIS, please make sure Network Service has the write access. If you're using Windows Azure, please modify the sample to output to a path in local storage. Then you can upload the output files to a public container in blob storage. MultiScaleImage is able to access a public container in blob storage.";
			}
		}

		/// <summary>
		/// Call this method if you choose to porvide your own deep zoom content.
		/// </summary>
		private void DisplayDeepZoom()
		{
			this.informationTextBlock.Text = "All done! Enjoy!";
			this.informationTextBlock.Visibility = Visibility.Collapsed;
			this.informationProgressBar.Visibility = Visibility.Collapsed;
			this.msi.Visibility = Visibility.Visible;
			string path = App.Current.Resources["path"].ToString();
			this.msi.Source = new DeepZoomImageTileSource(new Uri(path, UriKind.Relative));
			this._msiLoaded = true;
		}
	}
}