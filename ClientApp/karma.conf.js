// Karma configuration file, see link for more information
// https://karma-runner.github.io/1.0/config/configuration-file.html

module.exports = function (config) {
  config.set({
    basePath: '',
    frameworks: ['jasmine', '@angular-devkit/build-angular'],
    plugins: [
      require('karma-jasmine'),
      require('karma-chrome-launcher'),
      require('karma-jasmine-html-reporter'),
      require('karma-coverage'),
      require('karma-junit-reporter'),
      require('@angular-devkit/build-angular/plugins/karma')
    ],
    client: {
      jasmine: {
        // you can add configuration options for Jasmine here
        // the possible options are listed at https://jasmine.github.io/api/edge/Configuration.html
        // for example, you can disable the random execution with `random: false`
        // or set a specific seed with `seed: 4321`
      },
    },
    jasmineHtmlReporter: {
      suppressAll: true // removes the duplicated traces
    },
    // With `ng test --code-coverage` (CI: scripts/ci/frontend-test.sh). In test-results/ (git-ignored, CI artifact):
    // html to browse, cobertura for GitLab's MR coverage view, json-summary for the GitHub job summary,
    // text-summary in the log ("Lines : N%", read by GitLab's `coverage:` regex).
    coverageReporter: {
      dir: require('path').join(__dirname, './test-results/coverage'),
      subdir: '.',
      reporters: [
        { type: 'html' },
        { type: 'cobertura', file: 'cobertura.xml' },
        { type: 'json-summary' },
        { type: 'text-summary' }
      ]
    },
    // JUnit XML for the CI test reports (GitLab "Tests" tab, GitHub check run); written on every run.
    junitReporter: {
      outputDir: require('path').join(__dirname, './test-results'),
      outputFile: 'junit.xml',
      useBrowserName: false
    },
    reporters: ['progress', 'kjhtml', 'junit'],
    browsers: ['Chrome'],
    customLaunchers: {
      // CI containers run as root, where Chrome only starts without its sandbox.
      ChromeHeadlessCI: {
        base: 'ChromeHeadless',
        flags: ['--no-sandbox', '--disable-dev-shm-usage']
      }
    },
    restartOnFileChange: true
  });
};
