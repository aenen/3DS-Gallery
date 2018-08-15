var initPagination = function (page, pages, url, filters, dataSelector) {
  
  var paginationAjaxData = {
    dataElementSelector: dataSelector,
    urlParameters: filters,
    language: "en-US",

    beforeLoadPage: function () {
      loading(true);
      $('html, body').animate({ scrollTop: "0px" });
    },
    afterLoadPageSuccess: function () {
      initImg();
      loading(false);
    },
    afterLoadPageError: function () { loading(false); },

    paginationStyle: "allPagesShrink",
    paginationStyleFlexible: true,
    visiblePagesCount: 5
  };

  $("#pagination").paginationAjax(url, pages, paginationAjaxData);

  $(".filter").on("click", function () {
    loading(true);
    $(".filter-ul>.active").removeClass("active");
    $(this).parent().addClass("active");

    filters["filter"] = $(this).attr("value");
    var urlParams = $.param(filters);

    $("#picture-data").load(url+'?page=1&' + urlParams, function () {
      initImg();
      $("#pagination").paginationAjax(url, contentPages, $.extend(paginationAjaxData, { urlParameters: filters }));
      loading(false);
    });
  });
};

function loading(isLoading) {
  if (isLoading) {
    $("#loading").show();
    $(".filter-group > a.list-group-item").addClass("disabled");
    $(".pagination li:not(.active)").addClass("disabled");
  } else {
    $("#loading").hide();
    $(".filter-group > a.list-group-item").removeClass("disabled");
    $(".pagination li").removeClass("disabled");
  }
}