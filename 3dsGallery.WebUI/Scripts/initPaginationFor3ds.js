var initPagination = function (page, pages, url, filters, dataSelector, isPicture, paginationStyle) {
  var contentPages = pages;

  $(".filter").on("click", function (e) {
    if ($(this).parent().hasClass("active"))
      return false;
  });

  if (filters.hasOwnProperty("filter")) {
    $(".filter-ul>.active").removeClass("active");
    $(".filter[value='" + filters["filter"] + "']").parent().addClass("active");
  }

  /*
  можливі фільтри:
  - user:number
  - gallery:number
  - user_likes:bool
  - filter:string
  */
  
  createPagination(page, contentPages, url, filters);
  
  delete filters["filter"];
  var urlParams = $.param(filters);
  urlParams = urlParams ? "&" + urlParams : urlParams;
  $(".filter").each(function (i, e) {
    $(e).attr("href", url + "?page=1&filter=" + $(e).attr("value") + urlParams);
  });
}