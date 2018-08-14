(function ($) {

  /**
  * @summary Даний плагін створює пейджинг
  *
  * @param {string} pageDataUrl URL сторінки з даними.
  * @param {number} totalPages  Загальна кількість сторінок.
  * @param {object} options     Об'єкт з налаштуваннями (він може бути пустим).
  *
  * @return {object} Повертає jQuery об'єкт обраного елементу.
  */
  $.fn.paginationAjax = function (pageDataUrl, totalPages, options) {

    if (!pageDataUrl || typeof pageDataUrl !== "string") {
      console.error("Необхідний параметр {string} \"pageDataUrl\". Вказати URL сторінки з даними.");
      return this;
    }
    if (!totalPages || typeof totalPages !== "number") {
      console.error("Необхідний параметр {number} \"totalPages\". Вказати загальну кількість сторінок.");
      return this;
    }
    if (!options || typeof options !== "object") {
      console.error("Необхідний параметр {object} \"options\". Вказати об'єкт з налаштуваннями (він може бути пустим).");
      return this;
    }

    var thisElement = this;
    var style = getDefaultStyles();
    var lang = getDefaultLanguages();
    var defaultSettings = getDefaultSettings();
    var settings = $.extend({}, defaultSettings, options);

    if (style.hasOwnProperty(settings.paginationStyle)) {
      style[settings.paginationStyle].create(1, totalPages);
    } else {
      console.error("Стилю з назвою " + settings.paginationStyle + " не існує.");
      return this;
    }

    return this;



    /**
     * @summary Натиснення на елемент сторінки (ul.pagination li a.page).
     *
     * @param {object} e Об'єкт події.
     *
     * @returns {bool} False, якщо елемент сторінки має клас "active", або "disabled". Функція не виконується.
     */
    function pageClick(e) {
      if (settings.dontLoadActiveOrDisabledPage) {
        if ($(this).parent().hasClass("active") || $(this).parent().hasClass("disabled")) {
          return false;
        }
      }

      var page = $(this).data("page");

      settings.beforeLoadPage();
      loadPage(page, false, function () {
        style[settings.paginationStyle].afterLoadPage(page);
        settings.afterLoadPageSuccess();
      }, settings.afterLoadPageError);
    }

    /**
     * @summary Натиснення на кнопку "Завантажити ще" (#load-more).
     *
     * @param {object} e Об'єкт події
     *
     * @returns {bool} Повертає false, якщо елемент сторінки "активний" або "відключений" та функція не виконується
     */
    function loadMoreClick(e) {
      if (settings.dontLoadActiveOrDisabledPage) {
        if ($(this).hasClass("active") || $(this).hasClass("disabled")) {
          return false;
        }
      }

      var page = $("ul.pagination > li.active > a").last().data("page") + 1;

      settings.beforeLoadMore();
      loadPage(page, true, function () {
        style[settings.paginationStyle].afterLoadMore(page);
        settings.afterLoadMoreSuccess();
      }, settings.afterLoadMoreError);
    }

    /**
     * @summary Завантажує вказану сторінку та вставляє дані в контейнер.
     *
     * @param {number}   page      Номер сторінки, яку необхідно завантажити.
     * @param {boolean}  append    false - очищає контейнер перед вставкою; true - додає данні в кінець контейнеру.
     * @param {function} onSuccess Функція, яка виконується при успішному завантаженні даних.
     * @param {function} onError   Функція, яка виконується при не успішному завантаженні даних.
     */
    function loadPage(page, append, onSuccess, onError) {
      onSuccess = typeof onSuccess=="undefined"? $.noop : onSuccess;
      onError = typeof onError =="undefined"? $.noop : onError;
      var params = $.extend({ page: page }, settings.urlParameters);
      $.ajax({
        type: "GET",
        url: pageDataUrl + '?' + $.param(params),
        success: function (dataHtml) {
          if (!append) {
            $(settings.dataElementSelector).empty();
          }
          $(settings.dataElementSelector).append(dataHtml);

          onSuccess();
        },
        error: function () {
          onError();
        }
      });
    }

    function getDefaultLanguages() {
      return {
        "uk-UA": {
          "loadMore": "Завантажити ще"
        },
        "ru-RU": {
          "loadMore": "Загрузить еще"
        },
        "en-US": {
          "loadMore": "Load more"
        }
      };
    }

    /**
    * @summary Повертає об'єкт зі стандартними налаштуваннями.
    *
    * @returns {object} Oб'єкт зі стандартними налаштуваннями.
    */
    function getDefaultSettings() {
      return {
        dataElementSelector: "#pageData", // Елемент, в який завантажуються дані сторінки.
        urlParameters: {},                // Додаткові параметри сторінки, яка завантажується.
        language: "uk-UA",                // Мова плагіну.

        beforeLoadPage: $.noop,       // Метод виконується перед завантаженням обраної сторінки.
        afterLoadPageSuccess: $.noop, // Метод виконується після успішного завантаженням обраної сторінки.
        afterLoadPageError: $.noop,   // Метод виконується після не успішного завантаженням обраної сторінки.

        loadMoreButton: false,        // true - відображається кнопка "Завантажити ще", яка завантажує наступну сторінку.
        beforeLoadMore: $.noop,       // Метод виконується перед завантаженням наступної сторінки.
        afterLoadMoreSuccess: $.noop, // Метод виконується після успішного завантаженням наступної сторінки.
        afterLoadMoreError: $.noop,   // Метод виконується після не успішного завантаженням наступної сторінки.

        dontLoadActiveOrDisabledPage: true, // true - не завантажує сторінки з класами "active" чи "disabled".
        paginationStyle: "allPages",        // "allPages", "allPagesShrink".
        paginationStyleFlexible: false,     // true - кількість сторінок підлаштовується під розмір контейнеру.
        visiblePagesCount: 5               // Кількість сторінок, які відображаються (стилі: "allPagesShrink").
      };
    }

    /**
    * @summary Повертає об'єкт зі стандартними стилями.
    *
    * @returns {object} Oб'єкт зі стандартними стилями.
    */
    function getDefaultStyles() {

      return {

        /**
        * @summary Стиль відображає всі існуючі сторінки без додаткових кнопок.
        */
        "allPages": {

          /**
           * @summary Створення елементів "пейджингу" (ul.pagination).
           *
           * @param {number} currentPage Відображається сторінка з цим номером.
           * @param {number} totalPages  Загальна кількість сторінок.
           *
           * @returns {bool} Повертає false, якщо сторінок <= 1.
           */
          "create": function (currentPage, totalPages) {

            $(thisElement).empty();
            if (totalPages <= 1) {
              return false;
            }

            var container = $("<ul/>", { class: "pagination" }).css("display", "block");
            var pageElement = $("<a/>", { class: "page", 'data-page': 1 }).on("click", pageClick);

            for (var i = 1; i <= totalPages; i++) {
              var clonedPageElement = pageElement.clone(true).attr("data-page", i).text(i);
              container.append($("<li/>").append(clonedPageElement));
            }

            container.find("a[data-page='" + currentPage + "']").parent("li").addClass("active");
            $(thisElement).append(container);

            if (settings.loadMoreButton) {
              $("<button/>", {
                class: "btn btn-lg btn-primary",
                id: "load-more",
                text: lang[settings.language].loadMore
              }).on("click", loadMoreClick).insertBefore(thisElement);
            }
          },

          /**
           * @summary Метод виконується після успішного завантаження обраної сторінки.
           *
           * @param {number} loadedPage Завантажена сторінка.
           */
          "afterLoadPage": function (loadedPage) {

            var element = $("ul.pagination > li > a[data-page='" + loadedPage + "']");
            $("ul.pagination > li.active").removeClass("active");
            $(element).parent().addClass("active");

            if (settings.loadMoreButton) {
              if (+loadedPage === totalPages) {
                $("#load-more").hide();
              } else {
                $("#load-more").show();
              }
            }
          },

          /**
           * @summary Метод виконується після успішного завантаження наступної сторінки (кнопка "Завантажити ще").
           *
           * @param {number} loadedPage Завантажена сторінка.
           */
          "afterLoadMore": function (loadedPage) {

            $("ul.pagination > li.active").next().addClass("active");

            if (+loadedPage === totalPages) {
              $("#load-more").hide();
            }
          }
        },



        /**
        * @summary Стиль відображає всі існуючі сторінки, ховаючи деякі з них в "dropdown".
        *
        * @description  Відображаються кнопки "вперед"/"назад" при необхідності;
                        вказана кількість найближчих сторінок;
                        сторінки, які не помістились знаходяться в "dropdown" (клікабельні кнопки "...").
        */
        "allPagesShrink": {

          "data": {
            prevWidth: $(thisElement).width(),
            //visiblePages: $(thisElement).find("> ul.pagination > li:visible > a:not(.page-nav)").parent("li:visible").length
          },

          /**
           * @summary Створення елементів "пейджингу" (ul.pagination).
           *
           * @param {number} currentPage Відображається сторінка з цим номером.
           * @param {number} totalPages  Загальна кількість сторінок.
           *
           * @returns {bool} Повертає false, якщо сторінок <= 1.
           */
          "create": function (currentPage, totalPages) {

            $(thisElement).empty();
            if (totalPages <= 1) {
              return false;
            }
            
            // Шаблони елементів та контейнер:
            var container = $("<ul/>", { class: "pagination" }).css("display", "block");
            var pageElement = $("<a/>", { class: "page", 'data-page': 1 }).on("click", pageClick);
            var dropdown = $("<li/>")
              .append($("<div/>", { class: "dropup btn-group", style: "display:block" })
                .append($("<button/>", { class: "btn dropdown-toggle", type: "button", 'data-toggle': "dropdown", text: "..." })));

            // Данні по сторінкам:
            //style["allPagesShrink"].resize
            var visiblePages = settings.visiblePagesCount;
            var visiblePagesLeft = totalPages;
            var visiblePagesRight = totalPages;

            if (visiblePages >= totalPages) {
              visiblePages = totalPages;
            } else {
              if (visiblePages % 2 === 0) {
                visiblePagesLeft = visiblePages / 2 - 1;
                visiblePagesRight = visiblePages / 2;
              } else {
                visiblePagesLeft = visiblePagesRight = parseInt(visiblePages / 2);
              }
            }
            var pageFrom = currentPage - visiblePagesLeft > 0 ? currentPage - visiblePagesLeft : 1;
            var pageTo = currentPage + visiblePagesRight < totalPages ? currentPage + visiblePagesRight : totalPages;
            if (visiblePages < totalPages) {
              pageTo = currentPage - pageFrom < visiblePagesLeft ? pageTo + (visiblePagesLeft - currentPage + 1) : pageTo;
              pageFrom = pageTo - currentPage < visiblePagesRight ? pageFrom - (visiblePagesRight - (pageTo - currentPage)) : pageFrom;
            }

            // Якщо обрана сторінка не перша - створю кнопку "назад"
            if (currentPage > 1) {
              $("<li/>")
                .append(pageElement.clone(true).attr("data-page", currentPage - 1).addClass("page-nav page-prev").text("<"))
                .appendTo(container);
            }

            // Створюю дропдаун для попередніх сторінок, яких забагато
            createDropdown(1, pageFrom);

            // Створюю найближчі видимі кнопки сторінок
            for (var i = pageFrom; i <= pageTo; i++) {
              $("<li/>")
                .append(pageElement.clone(true).attr("data-page", i).text(i))
                .appendTo(container);
            }

            // Створюю дропдаун для наступних сторінок, яких забагато
            createDropdown(pageTo + 1, totalPages + 1);

            // Якщо обрана сторінка не остання - створюю кнопку "вперед"
            if (currentPage < totalPages) {
              $("<li/>")
                .append(pageElement.clone(true).attr("data-page", currentPage + 1).addClass("page-nav page-next").text(">"))
                .appendTo(container);
            }

            // Виділяю обрану сторінку як активну та додаю контейнер в елемент пейджингу
            container.find("a[data-page='" + currentPage + "']").parent("li").addClass("active");
            container.appendTo(thisElement);

            if (settings.loadMoreButton && currentPage !== totalPages) {
              $("<button/>", {
                class: "btn btn-lg btn-primary",
                id: "load-more",
                text: lang[settings.language].loadMore
              }).on("click", loadMoreClick).insertBefore($(thisElement).children("ul.pagination"));
            }

            if (settings.paginationStyleFlexible) {
              $(window).off("resize", style[settings.paginationStyle].resize);
              $(window).on("resize", style[settings.paginationStyle].resize);
              $(thisElement).trigger("resize", [true]);
            }

            /**
             * @summary Створює дродаун зі сторінками. 
             *
             * @param {number} from Починаючи з цієї сторінки.
             * @param {number} to   Закінчуючи цією сторінкою не включно (i < to)
             */
            function createDropdown(from, to) {
              var hiddenElementsList = $("<ul/>", { class: "dropdown-menu" });
              for (var i = from; i < to; i++) {
                hiddenElementsList
                  .append($("<li/>")
                    .append(pageElement.clone(true).attr("data-page", i).text(i)));
              }

              var clonedDropdown = dropdown.clone(true);
              clonedDropdown.appendTo(container).find("div.dropup").append(hiddenElementsList);
              if (!clonedDropdown.find(hiddenElementsList).children("li").length) {
                clonedDropdown.hide();
              }

            }
          },

          "resize": function (e, onCreate) {

            onCreate = typeof onCreate == "undefined" ? false : onCreate;
            var currentWidth = $(thisElement).width();

            if (style["allPagesShrink"].data.prevWidth === currentWidth && !onCreate) {
              return false;
            }

            var liPages = $(thisElement).find("> ul.pagination > li:visible > a:not(.page-nav)").parent("li:visible");
            var pages = $(thisElement).find("> ul.pagination > li:visible");
            var currentElement = $(thisElement).find("> ul.pagination > li.active:visible").last();
            var prevDropDown = $("ul.pagination li:has(div.dropup)").first();
            var nextDropDown = $("ul.pagination li:has(div.dropup)").last();
            
            if (onCreate) {
              pageBigger();
              pageSmaller();
            }

            // Якщо ширина сторінки зменшилась
            if (style["allPagesShrink"].data.prevWidth > currentWidth) {
              pageSmaller();
            } else {
              pageBigger();
            }
            
            style["allPagesShrink"].data.prevWidth = currentWidth;
            settings.visiblePagesCount = liPages.length;
                        
            function pageSmaller() {

              if (pages.last().position().top > pages.first().position().top) {

                while (pages.last().position().top > pages.first().position().top && liPages.length !== 1) {
                  var prevFromCurrent = currentElement.prevAll("li:has(> a:not(.page-nav))");
                  var nextFromCurrent = currentElement.nextAll("li:has(> a:not(.page-nav))");
                  var hideLeft = nextFromCurrent.length <= prevFromCurrent.length;

                  if (hideLeft) {
                    if (!currentElement.is(liPages.first())) {
                      prevDropDown.show().find("ul").append(liPages.first());
                    }
                  } else {
                    if (!currentElement.is(liPages.last())) {
                      nextDropDown.show().find("ul").prepend(liPages.last());
                    }
                  }

                  liPages = $(thisElement).find("> ul.pagination > li:visible > a:not(.page-nav)").parent("li:visible");
                  pages = $(thisElement).find("> ul.pagination > li:visible");
                }
              }
            }

            function pageBigger() {

              while (prevDropDown.find("ul > li").length || nextDropDown.find("ul > li").length) {
                var prevFromCurrent = currentElement.prevAll("li:has(> a:not(.page-nav))");
                var nextFromCurrent = currentElement.nextAll("li:has(> a:not(.page-nav))");
                
                var showFirst = nextFromCurrent.length > prevFromCurrent.length;
                showFirst = showFirst && !prevDropDown.find("ul > li").length ? false : showFirst;
                showFirst = !showFirst && !nextDropDown.find("ul > li").length ? true : showFirst;
                
                if (showFirst) {
                  var liFromDropDown = prevDropDown.find("ul > li").last().insertBefore(liPages.first());
                  pages = $(thisElement).find("> ul.pagination > li:visible");

                  if (pages.last().position().top > pages.first().position().top) {
                    prevDropDown.find("ul").append(liFromDropDown);
                    break;
                  }

                  if (!prevDropDown.find("ul > li").length) {
                    prevDropDown.hide();
                  }
                } else {
                  var liFromDropDown = nextDropDown.find("ul > li").first().insertAfter(liPages.last());
                  pages = $(thisElement).find("> ul.pagination > li:visible");

                  if (pages.last().position().top > pages.first().position().top) {
                    nextDropDown.find("ul").prepend(liFromDropDown);
                    break;
                  }

                  if (!nextDropDown.find("ul > li").length) {
                    nextDropDown.hide();
                  }
                }

                liPages = $(thisElement).find("> ul.pagination > li:visible > a:not(.page-nav)").parent("li:visible");
              }
            }
          },

          /**
           * @summary Метод виконується після успішного завантаження обраної сторінки.
           *
           * @param {number} loadedPage Завантажена сторінка.
           */
          "afterLoadPage": function (loadedPage) {

            style[settings.paginationStyle].create(loadedPage, totalPages);
          },

          /**
           * @summary Метод виконується після успішного завантаження наступної сторінки (кнопка "Завантажити ще").
           *
           * @param {number} loadedPage Завантажена сторінка.
           */
          "afterLoadMore": function (loadedPage) {
            var visiblePages = settings.visiblePagesCount;
            var visiblePagesLeft = totalPages;
            if (visiblePages >= totalPages) {
              visiblePages = totalPages;
            } else {
              if (visiblePages % 2 === 0) {
                visiblePagesLeft = visiblePages / 2 - 1;
              } else {
                visiblePagesLeft = parseInt(visiblePages / 2);
              }
            }

            var liElements = $("ul.pagination > li > a.page:not(.page-nav)").parent("li");

            if (loadedPage > visiblePagesLeft + 1) {
              var nextDrop = $("ul.pagination li:has(div.dropup)").last();
              nextDrop.find("ul > li").first().insertAfter(liElements.last());
              liElements = $("ul.pagination > li > a.page:not(.page-nav)").parent("li");

              if (!nextDrop.find("ul.dropdown-menu li").length) {
                nextDrop.hide();
              }
              if (liElements.length > visiblePages) {
                $("ul.pagination li:has(div.dropup)").first().show().find("ul").append(liElements.first());
              }

            }

            $("ul.pagination > li > a:not(.page-nav)[data-page='" + loadedPage + "']").parent("li").addClass("active");
            $("ul.pagination > li a.page-next").attr("data-page", loadedPage + 1);

            if (+loadedPage === totalPages) {
              $("#load-more").hide();
              $("ul.pagination > li a.page-next").parent("li").hide();
            }


            if (settings.paginationStyleFlexible) {
              $(thisElement).trigger("resize", [true]);
            }
          }
        }
      };
    }
  };

}(jQuery));