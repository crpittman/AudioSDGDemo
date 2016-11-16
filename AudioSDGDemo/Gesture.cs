using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using System.Windows;

namespace AudioSDGDemo
{
    class Gesture
    {

        public static int resample_cnt = 16;
        public static int r = 2;//(resample_cnt / 10);

        /*
        public static int resample_cnt = 16;
        public static int r = (resample_cnt / 10);
        */

        public string gname { get; set; }
        public List<Vector<float>> raw_pts;
        public List<Vector<float>> pts;
        public List<Vector<float>> vecs;
        public List<Vector<float>> lower, upper;

        public List<float> rejection;
        public float rejection_threshold;

        internal List<Vector<float>> features;
        internal float lower_bound;
        internal float fscore;

        
        public Gesture(List<Point> points, string label)
        {
            var temp = new List<Vector<float>>();
            foreach (Point p in points)
            {
                float[] data = { (float)p.X, (float)p.Y };
                temp.Add(Vector<float>.Build.Dense(data));
            }
            gname = label;
            raw_pts = temp;
            pts = Resample(temp, resample_cnt);
            vecs = new List<Vector<float>>();
            for (int ii = 1; ii < pts.Count; ii++)
            {
                vecs.Add(pts[ii].Subtract(pts[ii - 1]).Normalize(2));
            }

            features = Extract_Features(temp);


            Tuple<List<Vector<float>>, List<Vector<float>>> out_tuple = Envelop(vecs, r);
            lower = out_tuple.Item1;
            upper = out_tuple.Item2;

            rejection = new List<float>();
        }

        public Gesture(List<Vector<float>> temp, string label)
        {
            gname = label;
            raw_pts = temp;
            //pts = SmoothEveryFrame(Resample(temp, resample_cnt));
            pts = Resample(temp, resample_cnt);
            vecs = new List<Vector<float>>();
            for (int ii = 1; ii < pts.Count; ii++)
            {
                vecs.Add(pts[ii].Subtract(pts[ii - 1]).Normalize(2));
            }

            features = Extract_Features(temp);

            Tuple<List<Vector<float>>, List<Vector<float>>> out_tuple = Envelop(vecs, r);
            lower = out_tuple.Item1;
            upper = out_tuple.Item2;

            rejection = new List<float>();
        }

        private float PathLength(List<Vector<float>> points)
        {
            float length = 0;
            for (int ii = 1; ii < points.Count; ii++)
            {
                length += (float)points[ii].Subtract(points[ii - 1]).L2Norm();
            }
            return length;
        }

        private List<Vector<float>> Resample(List<Vector<float>> temp, int n)
        {
            List<Vector<float>> points = new List<Vector<float>>();
            foreach (Vector<float> v in temp)
                points.Add(Vector<float>.Build.DenseOfVector(v));
            List<Vector<float>> ret = new List<Vector<float>>();
            ret.Add(Vector<float>.Build.DenseOfVector(points[0]));
            float I = PathLength(points) / (n - 1.0f);
            float D = 0.0f;
            int ii = 1;
            while (ii < points.Count && I > 0)
            {
                float d = (float)points[ii].Subtract(points[ii - 1]).L2Norm();
                if (D + d >= I)
                {
                    Vector<float> vec = points[ii].Subtract(points[ii - 1]);
                    float t = (I - D) / d;

                    if (float.IsNaN(t))
                        t = 0.5f;

                    Vector<float> q = points[ii - 1] + t * vec;
                    ret.Add(q);
                    points.Insert(ii, q);
                    D = 0.0f;
                }
                else
                {
                    D += d;
                }
                ++ii;
            }
            while (ret.Count < n)
                ret.Add(points.Last());
            return ret;
        }

        public List<Vector<float>> StochasticResample(List<Vector<float>> temp, int n, int remove_cnt, float variance)
        {
            List<Vector<float>> points = new List<Vector<float>>();
            foreach (Vector<float> v in temp)
                points.Add(Vector<float>.Build.DenseOfVector(v));

            n += remove_cnt;
            float scale = (float)Math.Sqrt(12 * variance);
            Random rand = new Random();

            List<float> intervals = new List<float>(n-1);
            for (int jj = 0; jj < n - 1; ++jj)
            {
                intervals.Add(1.0f + (float)rand.NextDouble() * scale);
            }

            float total = intervals.Sum();
            intervals = intervals.Select(item => item / total).ToList();

            List<Vector<float>> new_points = new List<Vector<float>>();

            new_points.Add(Vector<float>.Build.DenseOfVector(points[0]));

            float PathDistance = PathLength(points);

            float I = PathDistance * intervals[0];
            float D = 0.0f;
            int ii = 1;
            while (ii < points.Count && I > 0)
            {
                float d = (float)points[ii].Subtract(points[ii - 1]).L2Norm();
                if (D + d >= I)
                {
                    Vector<float> vec = points[ii].Subtract(points[ii - 1]);
                    float t = (I - D) / d;

                    if (float.IsNaN(t))
                        t = 0.5f;

                    Vector<float> q = points[ii - 1] + t * vec;
                    new_points.Add(q);
                    points.Insert(ii, q);
                    D = 0.0f;
                    if (new_points.Count == n)
                        break;
                    I = PathDistance * intervals[new_points.Count - 1];
                }
                else
                {
                    D += d;
                }
                ++ii;
            }
            while (new_points.Count < n)
                new_points.Add(points.Last());

            for (int jj = 0; jj < remove_cnt; jj++)
                new_points.RemoveAt(rand.Next(new_points.Count - 1));

            var norm_pt = Vector<float>.Build.DenseOfVector(temp[0]);
            var ret = new List<Vector<float>>();
            ret.Add(norm_pt);
            I = PathDistance / n;

            for (int jj = 1; jj < n - remove_cnt; jj++)
            {
                var delta = new_points[jj] - new_points[jj - 1];
                var d = (float)delta.L2Norm();
                norm_pt = ret.Last() + (delta / d);
                ret.Add(norm_pt);
            }


            return ret;
        }

        private List<Vector<float>> SmoothEveryFrame(List<Vector<float>> pts)
        {
            var ret = new List<Vector<float>>();
            foreach (var pt in pts)
                ret.Add(SmoothFrame(pt));
            return ret;
        }
        private Vector<float> SmoothFrame(Vector<float> temp)
        {
            Vector<float> ret = Vector<float>.Build.DenseOfVector(temp);
            if (ret.Count < 5)
                return ret;

            ret[0] = (temp[0] + temp[1] + temp[2]) / 3;
            ret[1] = (temp[0] + temp[1] + temp[2] + temp[3]) / 4;
            ret[temp.Count - 1] = (temp[temp.Count - 1] + temp[temp.Count - 2] + temp[temp.Count - 3]) / 3;
            ret[temp.Count - 2] = (temp[temp.Count - 1] + temp[temp.Count - 2] + temp[temp.Count - 3] + temp[temp.Count - 4]) / 4;

            for (int ii = 2; ii < temp.Count - 2; ii++)
                ret[ii] = (temp[ii - 2] + temp[ii - 1] + temp[ii] + temp[ii + 1] + temp[ii + 2]) / 5;

            return ret;
        }

        private Tuple<List<Vector<float>>, List<Vector<float>>> Envelop(List<Vector<float>> vecs, int r)
        {
            int n = vecs.Count;
            int m = vecs[0].Count;

            List<Vector<float>> upper = new List<Vector<float>>();
            List<Vector<float>> lower = new List<Vector<float>>();

            for (int ii = 0; ii < n; ii++)
            {
                Vector<float> maximum = Vector<float>.Build.Dense(m, float.NegativeInfinity);
                Vector<float> minimum = Vector<float>.Build.Dense(m, float.PositiveInfinity);
                for (int jj = Math.Max(0, ii - r); jj < Math.Min(ii + r + 1, n); jj++)
                {
                    maximum = Maximum(maximum, vecs[jj]);
                    minimum = Minimum(minimum, vecs[jj]);
                }
                upper.Add(maximum);
                lower.Add(minimum);
            }

            return new Tuple<List<Vector<float>>, List<Vector<float>>>(lower, upper);
        }

        private Vector<float> Maximum(Vector<float> a, Vector<float> b)
        {
            Vector<float> max = Vector<float>.Build.SameAs(a);
            for (int ii = 0; ii < a.Count(); ii++)
                max[ii] = Math.Max(a[ii], b[ii]);
            return max;
        }
        private Vector<float> Minimum(Vector<float> a, Vector<float> b)
        {
            Vector<float> min = Vector<float>.Build.SameAs(a);
            for (int ii = 0; ii < a.Count(); ii++)
                min[ii] = Math.Min(a[ii], b[ii]);
            return min;
        }

        public int CompareTo(Gesture other)
        {
            return lower_bound.CompareTo(other.lower_bound);
        }

        static List<Vector<float>> Extract_Features(List<Vector<float>> points)
        {
            if (points.Count == 0)
                return null;

            var m = points[0].Count;
            var abs_dist = Vector<float>.Build.Dense(m, 0);

            //var emin = Vector<float>.Build.Dense(33, float.PositiveInfinity);
            //var emax = Vector<float>.Build.Dense(33, float.NegativeInfinity);

            for (int ii = 1;
                 ii < points.Count;
                 ii++)
            {
                var vec = points[ii] - points[ii - 1];
                Vector<float> point = points[ii];

                for (int jj = 0;
                     jj < m;
                     jj++)
                {
                    //int kk = jj % 33;
                    abs_dist[jj] += Math.Abs(vec[jj]);
                    //emin[kk] = Math.Min(emin[kk], Math.Abs(vec[jj]));
                    //emax[kk] = Math.Max(emax[kk], Math.Abs(vec[jj]));
                }
            }

            //var edeltas = emax - emin;

            return new List<Vector<float>> {
                abs_dist /  (float) abs_dist.L2Norm(),
                //edeltas / (float) edeltas.L2Norm(),
            };
        }

        public bool Is2D()
        {
            if (gname == "x" || gname == "c" || gname == "circle" || gname == "triangle"
                || gname == "rectangle" || gname == "check" || gname == "caret" || gname == "zigzag"
                || gname == "arrow" || gname == "star")
                return true;
            return false;
        }
    }
}
