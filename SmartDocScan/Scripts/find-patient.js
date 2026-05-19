var dTable;

var jsDataTable = '#tblPatient';

$(document).ready(function () {
    dTable = $(jsDataTable).DataTable({
        "sPaginationType": "full_numbers",
        "pageLength": 100,
        "bProcessing": true,
        "sAjaxSource": "/Admin/FindPatientBindGrid",
        oLanguage: { sProcessing: showLoader() },
        "fnDrawCallback": function (oSettings) {
            setTimeout(
                function () {
                    hideLoader();
                }, 1000);
        },
        "fnServerParams": function (aoData) {
            aoData.push({ "name": "companyId", "value": getParameterByName('id') });
            aoData.push({ "name": "searchText", "value": getPatientSearchText() });
        },
        aoColumns: [
            { "data": "last_name", "sClass": "text-center" },
            { "data": "first_name", "sClass": "text-center" },
            {
                "data": "dob",
                "type": "date ",
                "sClass": "text-center",
                "sWidth": "10%",
                "render": function (value) {
                    if (value === null) return "";
                    var pattern = /Date\(([^)]+)\)/;
                    var results = pattern.exec(value);
                    var dt = new Date(parseFloat(results[1]));
                    return (dt.getMonth() + 1) + "/" + dt.getDate() + "/" + dt.getFullYear();
                }
            },
            { "data": "pext_id", "sClass": "text-center" },
        ]
    });

    bindPatientSearch();

    $('#tblPatient tbody').on('click', 'tr', function () {
        var id = dTable.row(this).data().patient_id;
        window.location = "/admin/thumbdocument?id=" + getParameterByName("id") + "&pid=" + id;
    });

});

function getPatientSearchText() {
    var $searchInput = $('#tblPatient_filter input');
    return $searchInput.length ? $searchInput.val() : "";
}

function bindPatientSearch() {
    var searchTimer;
    $('#tblPatient_filter input').off('.DT').on('keyup search input paste cut', function () {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(function () {
            if (dTable && dTable.ajax && dTable.ajax.reload) {
                dTable.ajax.reload(null, false);
            }
        }, 300);
    });
}
