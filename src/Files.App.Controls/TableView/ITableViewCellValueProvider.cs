using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Files.App.Controls
{
	public interface ITableViewCellValueProvider
	{
		public string GetValue(string name);
	}
}
