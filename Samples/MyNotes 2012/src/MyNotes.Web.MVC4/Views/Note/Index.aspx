<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<IEnumerable<MyNotes.ReadModel.Types.Note>>" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	Index
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
	<%="" %>
	<h2>Index</h2>

	<table>
		<thead>
			<tr>
				<th scope="col"></th>
				<th scope="col">Creation Date</th>
				<th scope="col">Text</th>
			</tr>
		</thead>
		<tbody>
			<% foreach (var note in this.Model) { %>
			<tr>
				<td>
					<%: Html.ActionLink("Edit", "Edit", "Note", new {id = note.Id}, null)%>
				</td>
				<td>
					<%: String.Format("{0:g}", note.CreationDate) %>
				</td>
				<td>
					<%: note.Text %>
				</td>
			</tr>
			<% } %>
		</tbody>
	</table>

	<p>
		<%: Html.ActionLink("Add New", "Add", "Note") %>
	</p>
</asp:Content>
