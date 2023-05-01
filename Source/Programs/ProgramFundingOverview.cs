﻿using ferram4;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UniLinq;
using UnityEngine;
using UnityEngine.UI;

namespace RP0.Programs
{
    public class ProgramFundingOverview : MonoBehaviour
    {
        private ferramGraph _graph;
        private Program _program;

        private Image _imgFundingGraph = null;
        private Material _blitMataterial;

        internal void Awake()
        {
            // Create a Panel with a Layout Element
            var mainGraphAreaLE = gameObject.AddComponent<LayoutElement>();
            mainGraphAreaLE.preferredWidth = 500;

            // Create a HLG to control the elements below it
            var hLG = gameObject.AddComponent<HorizontalLayoutGroup>();
            hLG.childForceExpandWidth = false;
            hLG.childForceExpandHeight = true;
            hLG.spacing = 5;

            // Create the Y-Axis Label
            GameObject yAxisLabelGO = new GameObject("ProgramFundingGraphYAxis", typeof(RectTransform));
            yAxisLabelGO.transform.SetParent(mainGraphAreaLE.transform);
            yAxisLabelGO.transform.eulerAngles = new Vector3(0, 0, 90);
            yAxisLabelGO.gameObject.name = "FundingPerMonth";
            var yAxisLabelLE = yAxisLabelGO.gameObject.AddComponent<LayoutElement>();
            yAxisLabelLE.preferredWidth = 100;
            TextMeshProUGUI yAxisText = yAxisLabelGO.gameObject.AddComponent<TextMeshProUGUI>();
            yAxisText.alignment = TextAlignmentOptions.Midline;
            yAxisText.fontSizeMax = 15;
            yAxisText.fontSizeMin = 12;
            yAxisText.fontSize = 15;
            yAxisText.text = "<sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1> per Month";

            // Create the Graph and X-Axis Label Area
            GameObject graphRightPanelGO = new GameObject("ProgramFundingGraphLayout", typeof(RectTransform));
            graphRightPanelGO.transform.SetParent(mainGraphAreaLE.transform);
            graphRightPanelGO.name = "FundingGraphLayout";
            graphRightPanelGO.gameObject.AddComponent<LayoutElement>().preferredWidth = 395;
            var vLG = graphRightPanelGO.gameObject.AddComponent<VerticalLayoutGroup>();
            vLG.childForceExpandWidth = true;
            vLG.childForceExpandHeight = false;

            var fundingGraph = new GameObject("FundingGraph", typeof(RectTransform));
            fundingGraph.transform.SetParent(graphRightPanelGO.transform);
            fundingGraph.name = "FundingGraph";
            _imgFundingGraph = fundingGraph.gameObject.AddComponent<Image>();

            GameObject xAxisLabel = new GameObject("ProgramFundingGraphXAxis", typeof(RectTransform));
            xAxisLabel.transform.SetParent(graphRightPanelGO.transform);
            xAxisLabel.name = "FundingGraphYear";
            TextMeshProUGUI xAxisText = xAxisLabel.gameObject.AddComponent<TextMeshProUGUI>();
            xAxisText.alignment = TextAlignmentOptions.Midline;
            xAxisText.fontSizeMax = 15;
            xAxisText.fontSizeMin = 12;
            xAxisText.fontSize = 15;
            xAxisText.text = "Year";

            yAxisLabelGO.gameObject.SetActive(true);
            graphRightPanelGO.gameObject.SetActive(true);
            fundingGraph.gameObject.SetActive(true);
            xAxisLabel.gameObject.SetActive(true);

            gameObject.SetActive(false);

            _blitMataterial = new Material(Shader.Find("Unlit/Transparent"));

            InitGraph();
        }

        internal void OnDestroy()
        {
            _graph?.Dispose();
        }

        public void SetupProgram(Program program)
        {
            _program = program;
            if (program == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            var shownYears = Math.Ceiling(_program.DurationYears * 1.33);
            var points = (int)Math.Round(shownYears * 12);
            var xValues = new double[points];
            var yValues = new double[points];

            double totalPaid = 0;
            var xStep = shownYears / points;
            for (int i = 0; i < points; i++)
            {
                var x = i * xStep;

                const double secPerYear = 365.25d * 86400d;
                double fundAtX = _program.GetFundsAtTime(x * secPerYear);
                double paidThisPeriod = fundAtX - totalPaid;
                totalPaid = fundAtX;

                xValues[i] = x;
                yValues[i] = paidThisPeriod;
            }

            var data = new GraphData
            {
                xValues = xValues
            };
            data.AddData(yValues, XKCDColors.BrightCyan, "√", false);

            UpdateGraph(data, 0, shownYears, _program.ElapsedYears, _program.DurationYears);

            var fi = typeof(ferramGraph).GetField("graph", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var obj = fi.GetValue(_graph);
            var graphImage = (Texture2D)obj;

            fi = typeof(ferramGraph).GetField("allLines", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            obj = fi.GetValue(_graph);    // of type Dictionary<string, ferramGraphLine>

            RenderTexture oldRt = RenderTexture.active;
            RenderTexture rt = new RenderTexture(graphImage.width, graphImage.height, 0);
            RenderTexture.active = rt;
            Graphics.Blit(graphImage, rt);

            var dict = (IDictionary)obj;
            foreach (var val in dict.Values)
            {
                var mi = val.GetType().GetMethod("Line");
                var lineTexture = (Texture2D)mi.Invoke(val, new object[0]);
                Graphics.Blit(lineTexture, rt, _blitMataterial);
            }

            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            tex.Apply();
            rt.Release();
            RenderTexture.active = oldRt;

            Sprite newSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _imgFundingGraph.sprite = newSprite;
        }

        private void InitGraph()
        {
            _graph = new ferramGraph(400, 300);
            _graph.SetBoundaries(0, 10, 0, 2);
            _graph.SetGridScaleUsingValues(1, 1000);
            _graph.horizontalLabel = "Year";
            _graph.verticalLabel = "√ per month";
            _graph.Update();
        }

        private void UpdateGraph(GraphData data, double lowerBound, double upperBound, double elapsedDuration, double nominalDuration)
        {
            double newMinBounds = double.PositiveInfinity;
            double newMaxBounds = double.NegativeInfinity;

            foreach (double[] yValues in data.yValues)
            {
                newMinBounds = Math.Min(newMinBounds, yValues.Min());
                newMaxBounds = Math.Max(newMaxBounds, yValues.Max());
            }

            double realMin = Math.Min(Math.Floor(newMinBounds), 0);
            double realMax = Math.Max(Math.Ceiling(newMaxBounds), 1000);

            _graph.Clear();
            _graph.SetBoundaries(lowerBound, upperBound, realMin, realMax);
            _graph.SetGridScaleUsingValues(1, 1000);

            for (int i = 0; i < data.yValues.Count; i++)
                _graph.AddLine(data.lineNames[i],
                               data.xValues,
                               data.yValues[i],
                               data.lineColors[i],
                               1,
                               data.lineNameVisible[i]);

            _graph.AddLine("elapsedDuration",
                           new double[] { elapsedDuration, elapsedDuration },
                           new double[] { 0, realMax },
                           Color.green,
                           1,
                           false);

            _graph.AddLine("nominalDuration",
                           new double[] { nominalDuration, nominalDuration },
                           new double[] { 0, realMax },
                           Color.red,
                           1,
                           false);

            _graph.Update();
        }

        internal class GraphData
        {
            public readonly List<double[]> yValues;
            public readonly List<string> lineNames;
            public readonly List<bool> lineNameVisible;
            public readonly List<Color> lineColors;
            public double[] xValues;

            public GraphData()
            {
                yValues = new List<double[]>();
                lineNames = new List<string>();
                lineColors = new List<Color>();
                lineNameVisible = new List<bool>();
                xValues = null;
            }

            public void AddData(double[] yVals, Color lineColor, string name, bool nameVisible)
            {
                yValues.Add(yVals);
                lineColors.Add(lineColor);
                lineNames.Add(name);
                lineNameVisible.Add(nameVisible);
            }
        }
    }
}
