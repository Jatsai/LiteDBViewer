﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LiteDB;

namespace LiteDBViewer
{
    internal partial class MainForm : Form
    {
        private readonly LiteDatabase _db;

        public MainForm(string filename)
        {
            _db = new LiteDatabase(filename);
            InitializeComponent();
        }


        private void MainForm_Load(object sender, EventArgs e)
        {
            Text = Text.Replace("{FILENAME}", _db.GetDatabaseInfo().AsDocument.Get("filename"));
            foreach (var collection in _db.GetCollectionNames())
            {
                lb_Collections.Items.Add(collection);
            }
        }

        private void listBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lb_Collections.SelectedItem != null && !lb_Collections.SelectedItem.Equals("[QUERY]"))
            {
                FillDataGridView(_db.GetCollection(lb_Collections.SelectedItem.ToString()).Find(Query.All(), 0, 100));
                txt_query.Text = @"db." + lb_Collections.SelectedItem + @".find limit 100";
            }
        }

        public void FillDataGridView(IEnumerable<BsonDocument> documents)
        {
            if (lb_Collections.Items.Contains("[QUERY]"))
            {
                lb_Collections.Items.Remove("[QUERY]");
            }
            dataGridView.DataSource = null;
            if (documents != null)
            {
                var dt = new LiteDataTable(documents.ToString());
                foreach (var doc in documents)
                {
                    var dr = dt.NewRow() as LiteDataRow;
                    if (dr != null)
                    {
                        dr.UnderlyingValue = doc;
                        foreach (var property in doc.RawValue)
                        {
                            if (!property.Value.IsMaxValue && !property.Value.IsMinValue)
                            {
                                if (!dt.Columns.Contains(property.Key))
                                {
                                    dt.Columns.Add(new DataColumn(property.Key, typeof (string)));
                                }
                                switch (property.Value.Type)
                                {
                                    case BsonType.Null:
                                        dr[property.Key] = "[NULL]";
                                        break;
                                    case BsonType.Document:
                                        dr[property.Key] = "[OBJECT]";
                                        break;
                                    case BsonType.Array:
                                        dr[property.Key] = $"[ARRAY({property.Value.AsArray.Count})]";
                                        break;
                                    case BsonType.Binary:
                                        dr[property.Key] = $"[BINARY({property.Value.AsBinary.Length})]";
                                        break;
                                    default:
                                        dr[property.Key] = property.Value.ToString();
                                        break;
                                }
                            }
                        }
                        dt.Rows.Add(dr);
                    }
                }
                dataGridView.DataSource = dt;
            }
        }

        private void dataGridView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var currentMouseOver = dataGridView.HitTest(e.X, e.Y);
                if (currentMouseOver.RowIndex >= 0)
                {
                    var dataRowValue =
                        ((dataGridView.Rows[currentMouseOver.RowIndex].DataBoundItem as DataRowView)?.Row as
                            LiteDataRow)?.UnderlyingValue;
                    if (dataRowValue != null)
                    {
                        var cell = dataRowValue[dataGridView.Columns[currentMouseOver.ColumnIndex].Name];
                        var m = new ContextMenu();
                        switch (cell.Type)
                        {
                            case BsonType.String:
                                m.MenuItems.Add(new MenuItem("View String",
                                    (o, args) => new StringViewForm(cell).ShowDialog(this)));
                                break;
                            case BsonType.Document:
                                m.MenuItems.Add(new MenuItem("View Object",
                                    (o, args) => new DocumentViewForm(cell).ShowDialog(this)));
                                break;
                            case BsonType.Array:
                                m.MenuItems.Add(new MenuItem("View Array",
                                    (o, args) => new ArrayViewForm(cell).ShowDialog(this)));
                                break;
                            case BsonType.Binary:
                                m.MenuItems.Add(new MenuItem("View Binary",
                                    (o, args) => new BinaryViewForm(cell).ShowDialog(this)));
                                break;
                            default:
                                return;
                        }
                        m.Show(dataGridView, new Point(e.X, e.Y));
                    }
                }
            }
        }

        private void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                RunQuery(txt_query.Text);
                if (!lb_Collections.Items.Contains("[QUERY]"))
                {
                    lb_Collections.Items.Add("[QUERY]");
                }
                lb_Collections.SelectedItem = "[QUERY]";
            }
        }

        private void RunQuery(string query)
        {
            try
            {
                txt_query.Text = query;
                var result = _db.RunCommand(query);
                var rows = new List<BsonDocument>();
                if (result.IsArray)
                {
                    rows.AddRange(
                        result.AsArray.Select(
                            item => item.IsDocument ? item.AsDocument : new BsonDocument().Add("RESULT", result)));
                }
                else
                {
                    rows.Add(new BsonDocument().Add("RESULT", result));
                }
                FillDataGridView(rows);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, @"Bad Query", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}