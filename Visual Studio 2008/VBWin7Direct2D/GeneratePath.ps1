#****************************** Module Header ******************************\
# Module Name: GeneratePath.ps1
# Project:      VBWin7Direct2D
# Copyright (c) Microsoft Corporation.
# 
# The PowerShell script that translates XAML markup to VB code.
# 
# This source is subject to the Microsoft Public License.
# See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL.
# All other rights reserved.
# 
# History:
# * 10/11/2009 14:54 Yilun Luo Created
#***************************************************************************/

$invocation = (Get-Variable MyInvocation  -Scope 0).Value
$currentDir = Split-Path $invocation.InvocationName
cd $currentDir
$xmlData = [xml](Get-Content $args[0])
$segments = $xmlData.PathFigure.SelectNodes("BezierSegment")
$segments | ForEach-Object { "sink.AddBezier(New BezierSegment(New Point2F({0}!, {1}!), New Point2F({2}!, {3}!), New Point2F({4}!, {5}!)))" -f $_.GetAttribute("Point1").split(',')[0],
                     $_.GetAttribute("Point1").split(',')[1],
                     $_.GetAttribute("Point2").split(',')[0],
                     $_.GetAttribute("Point2").split(',')[1],
                     $_.GetAttribute("Point3").split(',')[0],
                     $_.GetAttribute("Point3").split(',')[1]
            } > $args[1]